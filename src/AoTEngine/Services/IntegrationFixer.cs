using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Text.RegularExpressions;

namespace AoTEngine.Services;

/// <summary>
/// Result of an integration fix attempt.
/// </summary>
public class IntegrationFixResult
{
    /// <summary>
    /// Whether fixes were applied successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The fixed code (if successful).
    /// </summary>
    public string FixedCode { get; set; } = string.Empty;

    /// <summary>
    /// Description of fixes applied.
    /// </summary>
    public List<string> AppliedFixes { get; set; } = new();

    /// <summary>
    /// Errors that could not be auto-fixed.
    /// </summary>
    public List<string> UnfixableErrors { get; set; } = new();

    /// <summary>
    /// Conflicts that require manual intervention.
    /// </summary>
    public List<TypeConflict> ManualInterventionRequired { get; set; } = new();
}

/// <summary>
/// Provides Roslyn-based auto-fix capabilities for common integration errors.
/// </summary>
public class IntegrationFixer
{
    private readonly TypeRegistry _typeRegistry;
    private readonly SymbolTable _symbolTable;
    private readonly Dictionary<string, string> _customTypeNamespaces;

    /// <summary>
    /// Default known namespace mappings for common .NET types.
    /// These are used when resolving missing using statements.
    /// Can be extended via constructor or AddTypeNamespaceMapping method.
    /// Note: Uses case-sensitive comparison since C# type names are case-sensitive.
    /// </summary>
    private static readonly Dictionary<string, string> DefaultKnownTypeNamespaces = new(StringComparer.Ordinal)
    {
        // Microsoft.Extensions types
        { "ILogger", "Microsoft.Extensions.Logging" },
        { "IConfiguration", "Microsoft.Extensions.Configuration" },
        { "IServiceCollection", "Microsoft.Extensions.DependencyInjection" },
        // System types
        { "Task", "System.Threading.Tasks" },
        { "List", "System.Collections.Generic" },
        { "Dictionary", "System.Collections.Generic" },
        { "IEnumerable", "System.Collections.Generic" },
        { "IReadOnlyList", "System.Collections.Generic" },
        { "Regex", "System.Text.RegularExpressions" },
        { "StringBuilder", "System.Text" },
        { "JsonSerializer", "System.Text.Json" },
        { "HttpClient", "System.Net.Http" }
    };

    public IntegrationFixer(TypeRegistry typeRegistry, SymbolTable symbolTable)
        : this(typeRegistry, symbolTable, null)
    {
    }

    /// <summary>
    /// Creates an IntegrationFixer with custom type-to-namespace mappings.
    /// </summary>
    /// <param name="typeRegistry">The type registry.</param>
    /// <param name="symbolTable">The symbol table.</param>
    /// <param name="customTypeNamespaces">Custom mappings to add/override default mappings.</param>
    public IntegrationFixer(TypeRegistry typeRegistry, SymbolTable symbolTable, Dictionary<string, string>? customTypeNamespaces)
    {
        _typeRegistry = typeRegistry;
        _symbolTable = symbolTable;
        // Use case-sensitive comparison since C# type names are case-sensitive
        _customTypeNamespaces = customTypeNamespaces ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Adds or updates a type-to-namespace mapping for missing using resolution.
    /// </summary>
    public void AddTypeNamespaceMapping(string typeName, string namespaceName)
    {
        _customTypeNamespaces[typeName] = namespaceName;
    }

    /// <summary>
    /// Gets the namespace for a type name, checking custom mappings first, then defaults.
    /// </summary>
    private bool TryGetTypeNamespace(string typeName, out string? namespaceName)
    {
        if (_customTypeNamespaces.TryGetValue(typeName, out var customNamespace))
        {
            namespaceName = customNamespace;
            return true;
        }

        if (DefaultKnownTypeNamespaces.TryGetValue(typeName, out var defaultNamespace))
        {
            namespaceName = defaultNamespace;
            return true;
        }

        namespaceName = null;
        return false;
    }

    /// <summary>
    /// Attempts to fix integration errors in the provided code.
    /// </summary>
    /// <param name="code">The code with potential issues.</param>
    /// <param name="diagnostics">Roslyn diagnostics from compilation.</param>
    /// <returns>Result indicating success and applied fixes.</returns>
    public IntegrationFixResult TryFix(string code, IEnumerable<Diagnostic> diagnostics)
    {
        var result = new IntegrationFixResult { FixedCode = code };
        var diagnosticList = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        if (!diagnosticList.Any())
        {
            result.Success = true;
            return result;
        }

        var currentCode = code;
        var appliedFixes = new List<string>();
        var unfixableErrors = new List<string>();

        // Classify diagnostics into fixable buckets
        foreach (var diagnostic in diagnosticList)
        {
            var message = diagnostic.GetMessage();
            var location = diagnostic.Location;

            // Try to apply auto-fixes
            var fixResult = TryApplyFix(currentCode, diagnostic.Id, message, location);
            
            if (fixResult.Applied)
            {
                currentCode = fixResult.NewCode;
                appliedFixes.Add(fixResult.Description);
            }
            else
            {
                unfixableErrors.Add($"{diagnostic.Id}: {message}");
            }
        }

        result.FixedCode = currentCode;
        result.AppliedFixes = appliedFixes;
        result.UnfixableErrors = unfixableErrors;
        result.Success = !unfixableErrors.Any();

        return result;
    }

    /// <summary>
    /// Removes duplicate type definitions from code based on type registry conflicts.
    /// </summary>
    public string RemoveDuplicateTypes(string code, List<TypeConflict> conflicts)
    {
        if (!conflicts.Any())
            return code;

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        var nodesToRemove = new List<SyntaxNode>();

        foreach (var conflict in conflicts.Where(c => c.SuggestedResolution == ConflictResolution.RemoveDuplicate ||
                                                       c.SuggestedResolution == ConflictResolution.KeepFirst))
        {
            // Find the duplicate type declaration from the "new" entry
            var typeDeclarations = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(t => t.Identifier.Text == conflict.NewEntry.TypeName);

            foreach (var typeDecl in typeDeclarations)
            {
                var ns = GetNamespace(typeDecl);
                if (ns == conflict.NewEntry.Namespace)
                {
                    nodesToRemove.Add(typeDecl);
                }
            }
        }

        if (!nodesToRemove.Any())
            return code;

        var newRoot = root.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia);
        return newRoot?.ToFullString() ?? code;
    }

    /// <summary>
    /// Converts duplicate class definitions to partial classes if possible.
    /// </summary>
    public string ConvertToPartialClasses(string code, List<TypeConflict> conflicts)
    {
        var partialConflicts = conflicts
            .Where(c => c.SuggestedResolution == ConflictResolution.MergeAsPartial)
            .ToList();

        if (!partialConflicts.Any())
            return code;

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();

        var typeNamesToConvert = partialConflicts
            .Select(c => c.FullyQualifiedName)
            .ToHashSet();

        // Find all class declarations that need to become partial
        var classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .ToList();

        // Use ReplaceNodes to handle all replacements at once, avoiding the issue
        // of old nodes no longer being part of the tree after individual replacements
        var newRoot = root.ReplaceNodes(
            classDeclarations,
            (original, rewritten) =>
            {
                var ns = GetNamespace(original);
                var fullName = string.IsNullOrEmpty(ns)
                    ? original.Identifier.Text
                    : $"{ns}.{original.Identifier.Text}";

                if (typeNamesToConvert.Contains(fullName) && !original.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    // Add partial modifier
                    var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                        .WithTrailingTrivia(SyntaxFactory.Space);
                    var newModifiers = original.Modifiers.Add(partialToken);
                    return original.WithModifiers(newModifiers);
                }

                return original;
            });

        return newRoot?.ToFullString() ?? code;
    }

    /// <summary>
    /// Adds missing using statements based on error analysis.
    /// </summary>
    public string AddMissingUsings(string code, List<string> missingNamespaces)
    {
        if (!missingNamespaces.Any())
            return code;

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = (CompilationUnitSyntax)syntaxTree.GetRoot();

        // Get existing usings
        var existingUsings = root.Usings
            .Select(u => u.Name?.ToString() ?? string.Empty)
            .ToHashSet();

        // Filter out duplicates
        var newUsings = missingNamespaces
            .Where(ns => !existingUsings.Contains(ns))
            .Distinct()
            .Select(ns => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed))
            .ToList();

        if (!newUsings.Any())
            return code;

        var newRoot = root.AddUsings(newUsings.ToArray());
        return newRoot.ToFullString();
    }

    /// <summary>
    /// Number of characters to look back when checking for namespace declaration context.
    /// </summary>
    private const int NamespaceContextSearchLength = 20;

    /// <summary>
    /// Resolves ambiguous type references by fully qualifying them.
    /// Note: This method uses a regex-based heuristic to update type references and may not handle all
    /// edge cases correctly (for example, complex generics, aliasing, or conditional compilation).
    /// For more precise resolution, a Roslyn semantic model-based approach would be needed.
    /// </summary>
    public string ResolveAmbiguousReferences(string code, Dictionary<string, string> ambiguousTypeToFullName)
    {
        if (!ambiguousTypeToFullName.Any())
            return code;

        var result = code;
        foreach (var (simpleName, fullyQualified) in ambiguousTypeToFullName)
        {
            // Use a simpler pattern that avoids variable-length lookbehinds (not supported in .NET regex).
            // This pattern matches the type name as a whole word, not preceded by a dot.
            // (?<!\.)           - Not preceded by a dot (to avoid already-qualified names)
            // \b                - Word boundary
            // {escapedName}     - The type name we're replacing
            // \b                - Word boundary (ensures we match complete identifiers)
            var pattern = $@"(?<!\.)\b{Regex.Escape(simpleName)}\b";
            result = Regex.Replace(result, pattern, match =>
            {
                // Skip if this appears to be in a namespace declaration context
                var startIndex = Math.Max(0, match.Index - NamespaceContextSearchLength);
                var length = Math.Min(NamespaceContextSearchLength, match.Index);
                var prefix = result.Substring(startIndex, length);
                if (prefix.Contains("namespace "))
                    return match.Value;
                return fullyQualified;
            });
        }

        return result;
    }

    /// <summary>
    /// Removes duplicate members from a class definition.
    /// </summary>
    public string RemoveDuplicateMembers(string code, string className, List<MemberSignature> duplicates)
    {
        if (!duplicates.Any())
            return code;

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();

        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);

        if (classDecl == null)
            return code;

        var membersToRemove = new List<MemberDeclarationSyntax>();
        var seenSignatures = new HashSet<string>();

        foreach (var member in classDecl.Members)
        {
            var signature = GetMemberSignature(member);
            if (signature != null)
            {
                if (seenSignatures.Contains(signature))
                {
                    membersToRemove.Add(member);
                }
                else
                {
                    seenSignatures.Add(signature);
                }
            }
        }

        if (!membersToRemove.Any())
            return code;

        var newClassDecl = classDecl.RemoveNodes(membersToRemove, SyntaxRemoveOptions.KeepNoTrivia);
        var newRoot = root.ReplaceNode(classDecl, newClassDecl!);
        return newRoot.ToFullString();
    }

    private (bool Applied, string NewCode, string Description) TryApplyFix(
        string code, string diagnosticId, string message, Location location)
    {
        // CS0246: The type or namespace name 'X' could not be found
        if (diagnosticId == "CS0246")
        {
            return TryFixMissingType(code, message);
        }

        // CS0234: The type or namespace name 'X' does not exist in the namespace 'Y'
        if (diagnosticId == "CS0234")
        {
            return TryFixMissingNamespace(code, message);
        }

        // CS0104: 'X' is an ambiguous reference
        if (diagnosticId == "CS0104")
        {
            return TryFixAmbiguousReference(code, message, location);
        }

        // CS0111: Type already defines a member with same parameter types
        if (diagnosticId == "CS0111")
        {
            return TryFixDuplicateMember(code, message, location);
        }

        // CS0101: The namespace already contains a definition for 'X'
        if (diagnosticId == "CS0101")
        {
            return TryFixDuplicateType(code, message, location);
        }

        // CS1503: Cannot convert from 'X' to 'Y' (type mismatch)
        if (diagnosticId == "CS1503")
        {
            return TryFixTypeMismatch(code, message, location);
        }

        // CS1729: 'X' does not contain a constructor that takes N arguments
        if (diagnosticId == "CS1729")
        {
            // This is generally not auto-fixable without knowing the right approach
            return (false, code, string.Empty);
        }

        return (false, code, string.Empty);
    }

    private (bool Applied, string NewCode, string Description) TryFixMissingType(string code, string message)
    {
        // Extract type name from message
        var match = Regex.Match(message, @"The type or namespace name '([^']+)' could not be found");
        if (!match.Success)
            return (false, code, string.Empty);

        var typeName = match.Groups[1].Value;

        // Check if we know the namespace for this type (custom mappings first, then defaults)
        if (TryGetTypeNamespace(typeName, out var ns) && ns != null)
        {
            var fixedCode = AddMissingUsings(code, new List<string> { ns });
            if (fixedCode != code)
            {
                return (true, fixedCode, $"Added using {ns} for type {typeName}");
            }
        }

        // Check the type registry for known types
        var matchingTypes = _typeRegistry.GetTypesBySimpleName(typeName);
        if (matchingTypes.Count == 1)
        {
            var typeEntry = matchingTypes[0];
            var fixedCode = AddMissingUsings(code, new List<string> { typeEntry.Namespace });
            if (fixedCode != code)
            {
                return (true, fixedCode, $"Added using {typeEntry.Namespace} for type {typeName}");
            }
        }

        return (false, code, string.Empty);
    }

    private (bool Applied, string NewCode, string Description) TryFixMissingNamespace(string code, string message)
    {
        // Extract namespace from message
        var match = Regex.Match(message, @"does not exist in the namespace '([^']+)'");
        if (!match.Success)
            return (false, code, string.Empty);

        var parentNs = match.Groups[1].Value;

        // Try adding the parent namespace as a using
        var fixedCode = AddMissingUsings(code, new List<string> { parentNs });
        if (fixedCode != code)
        {
            return (true, fixedCode, $"Added using {parentNs}");
        }

        return (false, code, string.Empty);
    }

    private (bool Applied, string NewCode, string Description) TryFixAmbiguousReference(
        string code, string message, Location location)
    {
        // Extract type name from message
        var match = Regex.Match(message, @"'([^']+)' is an ambiguous reference");
        if (!match.Success)
            return (false, code, string.Empty);

        var typeName = match.Groups[1].Value;

        // Check type registry for possible qualified names
        var matchingTypes = _typeRegistry.GetTypesBySimpleName(typeName);
        if (matchingTypes.Count > 0)
        {
            // Pick the first one (or use heuristics)
            var preferredType = matchingTypes[0];
            var fixedCode = ResolveAmbiguousReferences(code, 
                new Dictionary<string, string> { { typeName, preferredType.FullyQualifiedName } });
            
            if (fixedCode != code)
            {
                return (true, fixedCode, $"Fully qualified {typeName} to {preferredType.FullyQualifiedName}");
            }
        }

        return (false, code, string.Empty);
    }

    private (bool Applied, string NewCode, string Description) TryFixDuplicateMember(
        string code, string message, Location location)
    {
        // This fix removes the duplicate member at the error location
        var lineSpan = location.GetLineSpan();
        if (!lineSpan.IsValid)
            return (false, code, string.Empty);

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        var node = root.FindNode(location.SourceSpan);

        if (node is MemberDeclarationSyntax memberDecl)
        {
            var newRoot = root.RemoveNode(memberDecl, SyntaxRemoveOptions.KeepNoTrivia);
            if (newRoot != null)
            {
                return (true, newRoot.ToFullString(), $"Removed duplicate member at line {lineSpan.StartLinePosition.Line + 1}");
            }
        }

        return (false, code, string.Empty);
    }

    private (bool Applied, string NewCode, string Description) TryFixDuplicateType(
        string code, string message, Location location)
    {
        // Extract type name
        var match = Regex.Match(message, @"already contains a definition for '([^']+)'");
        if (!match.Success)
            return (false, code, string.Empty);

        var typeName = match.Groups[1].Value;

        // Find and remove the duplicate type at the error location
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        var node = root.FindNode(location.SourceSpan);

        // Navigate to the type declaration
        var typeDecl = node.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl != null && typeDecl.Identifier.Text == typeName)
        {
            var newRoot = root.RemoveNode(typeDecl, SyntaxRemoveOptions.KeepNoTrivia);
            if (newRoot != null)
            {
                return (true, newRoot.ToFullString(), $"Removed duplicate type {typeName}");
            }
        }

        return (false, code, string.Empty);
    }

    private (bool Applied, string NewCode, string Description) TryFixTypeMismatch(
        string code, string message, Location location)
    {
        // Check if it's an IReadOnlyList vs IEnumerable mismatch
        if (message.Contains("IReadOnlyList") && message.Contains("IEnumerable"))
        {
            // This usually means the types are from different namespaces
            // Auto-fix: Add .AsEnumerable() or use LINQ
            // For now, mark as unfixable as it requires semantic understanding
            return (false, code, string.Empty);
        }

        return (false, code, string.Empty);
    }

    private string? GetNamespace(SyntaxNode node)
    {
        var nsDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return nsDecl?.Name.ToString();
    }

    private string? GetMemberSignature(MemberDeclarationSyntax member)
    {
        return member switch
        {
            ConstructorDeclarationSyntax ctor => 
                $"ctor({string.Join(",", ctor.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? ""))})",
            MethodDeclarationSyntax method => 
                $"{method.Identifier.Text}({string.Join(",", method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? ""))})",
            PropertyDeclarationSyntax prop => prop.Identifier.Text,
            FieldDeclarationSyntax field => 
                string.Join(",", field.Declaration.Variables.Select(v => v.Identifier.Text)),
            _ => null
        };
    }
}
