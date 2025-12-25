using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AoTEngine.Services;

/// <summary>
/// Extended integration fixer with auto-fix loop capabilities for interface implementations,
/// abstract method overrides, enum governance, and sealed type handling.
/// </summary>
public class AutoFixService
{
    private readonly TypeRegistry _typeRegistry;
    private readonly SymbolTable _symbolTable;
    private readonly ContractCatalog? _contractCatalog;
    private readonly IntegrationFixer _baseFixer;
    private const int MaxFixAttempts = 3;

    public AutoFixService(
        TypeRegistry typeRegistry,
        SymbolTable symbolTable,
        ContractCatalog? contractCatalog = null)
    {
        _typeRegistry = typeRegistry;
        _symbolTable = symbolTable;
        _contractCatalog = contractCatalog;
        _baseFixer = new IntegrationFixer(typeRegistry, symbolTable);
    }

    /// <summary>
    /// Attempts to auto-fix all compilation errors in a feedback loop.
    /// </summary>
    /// <param name="code">Code with errors.</param>
    /// <param name="diagnostics">Compilation diagnostics.</param>
    /// <param name="references">Metadata references for compilation.</param>
    /// <returns>Fix result with patched code or remaining errors.</returns>
    public async Task<AutoFixResult> AutoFixLoopAsync(
        string code,
        IEnumerable<Diagnostic> diagnostics,
        List<MetadataReference> references)
    {
        var result = new AutoFixResult { OriginalCode = code };
        var currentCode = code;
        var appliedFixes = new List<string>();
        var remainingErrors = new List<string>();

        for (int attempt = 0; attempt < MaxFixAttempts; attempt++)
        {
            var diagnosticList = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (!diagnosticList.Any())
            {
                result.Success = true;
                result.FixedCode = currentCode;
                result.AppliedFixes = appliedFixes;
                return result;
            }

            Console.WriteLine($"   Auto-fix attempt {attempt + 1}: {diagnosticList.Count} errors to fix");

            var fixApplied = false;

            // Try to fix each diagnostic
            foreach (var diagnostic in diagnosticList)
            {
                var fixResult = TryFixDiagnostic(currentCode, diagnostic);
                
                if (fixResult.Applied)
                {
                    currentCode = fixResult.NewCode;
                    appliedFixes.Add(fixResult.Description);
                    fixApplied = true;
                }
            }

            if (!fixApplied)
            {
                // No fixes could be applied, collect remaining errors
                foreach (var diag in diagnosticList)
                {
                    remainingErrors.Add($"{diag.Id}: {diag.GetMessage()}");
                }
                break;
            }

            // Recompile to get new diagnostics
            var syntaxTree = CSharpSyntaxTree.ParseText(currentCode);
            var compilation = CSharpCompilation.Create(
                $"AutoFixCheck_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            diagnostics = compilation.GetDiagnostics();
        }

        result.FixedCode = currentCode;
        result.AppliedFixes = appliedFixes;
        result.RemainingErrors = remainingErrors;
        result.Success = !remainingErrors.Any();

        return result;
    }

    /// <summary>
    /// Tries to fix a single diagnostic.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixDiagnostic(
        string code, Diagnostic diagnostic)
    {
        var diagId = diagnostic.Id;
        var message = diagnostic.GetMessage();
        var location = diagnostic.Location;

        return diagId switch
        {
            // Missing interface member
            "CS0535" => TryFixMissingInterfaceMember(code, message, location),
            
            // Missing abstract member override
            "CS0534" => TryFixMissingAbstractMember(code, message, location),
            
            // Return type mismatch
            "CS0508" => TryFixReturnTypeMismatch(code, message, location),
            
            // Cannot derive from sealed type
            "CS0509" => TryFixSealedInheritance(code, message, location),
            
            // Ambiguous reference
            "CS0104" => TryFixAmbiguousReference(code, message, location),
            
            // Missing using
            "CS0246" => TryFixMissingUsing(code, message),
            
            // Missing namespace
            "CS0234" => TryFixMissingNamespace(code, message),
            
            // Duplicate type definition
            "CS0101" => TryFixDuplicateType(code, message, location),
            
            // Duplicate member
            "CS0111" => TryFixDuplicateMember(code, message, location),
            
            _ => (false, code, string.Empty)
        };
    }

    /// <summary>
    /// Fixes missing interface member by generating a stub implementation.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixMissingInterfaceMember(
        string code, string message, Location location)
    {
        // Extract interface and member from message
        // Pattern: "'ClassName' does not implement interface member 'InterfaceName.MethodName(params)'"
        var match = Regex.Match(message, @"'([^']+)' does not implement interface member '([^']+)\.([^']+)'");
        
        if (!match.Success)
        {
            return (false, code, string.Empty);
        }

        var className = match.Groups[1].Value;
        var interfaceName = match.Groups[2].Value;
        var memberSignature = match.Groups[3].Value;

        // Get the interface contract if available
        if (_contractCatalog != null)
        {
            var interfaceContract = _contractCatalog.Interfaces.FirstOrDefault(i => 
                i.Name == interfaceName || i.FullyQualifiedName == interfaceName);

            if (interfaceContract != null)
            {
                var methodName = memberSignature.Split('(')[0];
                var method = interfaceContract.Methods.FirstOrDefault(m => m.Name == methodName);

                if (method != null)
                {
                    // Generate implementation stub
                    var stub = GenerateMethodStub(method, isOverride: false);
                    var newCode = InsertMethodIntoClass(code, className, stub);
                    
                    if (newCode != code)
                    {
                        return (true, newCode, $"Added stub implementation for {interfaceName}.{methodName}");
                    }
                }
            }
        }

        // Fallback: generate a basic stub
        var basicStub = $"    public {memberSignature} => throw new NotImplementedException();";
        var fallbackCode = InsertMethodIntoClass(code, className, basicStub);
        
        if (fallbackCode != code)
        {
            return (true, fallbackCode, $"Added stub for missing interface member {memberSignature}");
        }

        return (false, code, string.Empty);
    }

    /// <summary>
    /// Fixes missing abstract member by generating an override.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixMissingAbstractMember(
        string code, string message, Location location)
    {
        // Pattern: "'ClassName' does not implement inherited abstract member 'BaseClass.Method()'"
        var match = Regex.Match(message, @"'([^']+)' does not implement inherited abstract member '([^']+)\.([^']+)'");
        
        if (!match.Success)
        {
            return (false, code, string.Empty);
        }

        var className = match.Groups[1].Value;
        var baseClassName = match.Groups[2].Value;
        var memberSignature = match.Groups[3].Value;

        // Get the abstract class contract if available
        if (_contractCatalog != null)
        {
            var abstractContract = _contractCatalog.AbstractClasses.FirstOrDefault(a => 
                a.Name == baseClassName || a.FullyQualifiedName == baseClassName);

            if (abstractContract != null)
            {
                var methodName = memberSignature.Split('(')[0];
                var method = abstractContract.AbstractMethods.FirstOrDefault(m => m.Name == methodName);

                if (method != null)
                {
                    // Generate override stub
                    var stub = GenerateMethodStub(method, isOverride: true);
                    var newCode = InsertMethodIntoClass(code, className, stub);
                    
                    if (newCode != code)
                    {
                        return (true, newCode, $"Added override for {baseClassName}.{methodName}");
                    }
                }
            }
        }

        return (false, code, string.Empty);
    }

    /// <summary>
    /// Fixes return type mismatch.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixReturnTypeMismatch(
        string code, string message, Location location)
    {
        // Pattern: "return type must be 'ExpectedType' to match overridden member 'Class.Method'"
        var match = Regex.Match(message, @"return type must be '([^']+)' to match");
        
        if (!match.Success)
        {
            return (false, code, string.Empty);
        }

        var expectedType = match.Groups[1].Value;

        // Find the method at the error location and fix its return type
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        var node = root.FindNode(location.SourceSpan);

        if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is { } method)
        {
            var newReturnType = SyntaxFactory.ParseTypeName(expectedType)
                .WithTrailingTrivia(SyntaxFactory.Space);
            var newMethod = method.WithReturnType(newReturnType);
            var newRoot = root.ReplaceNode(method, newMethod);
            
            return (true, newRoot.ToFullString(), $"Fixed return type to {expectedType}");
        }

        return (false, code, string.Empty);
    }

    /// <summary>
    /// Fixes inheritance from sealed type by converting to composition.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixSealedInheritance(
        string code, string message, Location location)
    {
        // Pattern: "'DerivedClass': cannot derive from sealed type 'SealedClass'"
        var match = Regex.Match(message, @"'([^']+)': cannot derive from sealed type '([^']+)'");
        
        if (!match.Success)
        {
            return (false, code, string.Empty);
        }

        var derivedClass = match.Groups[1].Value;
        var sealedClass = match.Groups[2].Value;

        // Find the class declaration and remove the inheritance
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == derivedClass);

        if (classDecl == null || classDecl.BaseList == null)
        {
            return (false, code, string.Empty);
        }

        // Remove the sealed base type from inheritance
        var sealedTypeName = sealedClass.Split('.').Last();
        var newBaseTypes = classDecl.BaseList.Types
            .Where(t => !t.Type.ToString().Contains(sealedTypeName))
            .ToList();

        ClassDeclarationSyntax newClassDecl;
        
        if (!newBaseTypes.Any())
        {
            // Remove base list entirely
            newClassDecl = classDecl.WithBaseList(null);
        }
        else
        {
            // Keep remaining base types
            var newBaseList = SyntaxFactory.BaseList(
                SyntaxFactory.SeparatedList(newBaseTypes));
            newClassDecl = classDecl.WithBaseList(newBaseList);
        }

        // Add composition field
        var fieldName = $"_{char.ToLower(sealedTypeName[0])}{sealedTypeName.Substring(1)}";
        var field = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.ParseTypeName(sealedClass))
            .WithVariables(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(fieldName))))
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

        var membersWithField = newClassDecl.Members.Insert(0, field);
        newClassDecl = newClassDecl.WithMembers(membersWithField);

        var newRoot = root.ReplaceNode(classDecl, newClassDecl);
        
        return (true, newRoot.ToFullString(), 
            $"Converted inheritance from sealed '{sealedClass}' to composition");
    }

    /// <summary>
    /// Fixes ambiguous reference by fully qualifying the type.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixAmbiguousReference(
        string code, string message, Location location)
    {
        var match = Regex.Match(message, @"'([^']+)' is an ambiguous reference");
        
        if (!match.Success)
        {
            return (false, code, string.Empty);
        }

        var typeName = match.Groups[1].Value;

        // Get the preferred fully qualified name
        var matchingTypes = _typeRegistry.GetTypesBySimpleName(typeName);
        if (!matchingTypes.Any())
        {
            // Check contract catalog
            if (_contractCatalog != null)
            {
                var contract = _contractCatalog.GetContract(typeName);
                if (contract != null)
                {
                    var fqn = contract.FullyQualifiedName;
                    var newCode = _baseFixer.ResolveAmbiguousReferences(code, 
                        new Dictionary<string, string> { { typeName, fqn } });
                    
                    if (newCode != code)
                    {
                        return (true, newCode, $"Fully qualified {typeName} to {fqn}");
                    }
                }
            }
            return (false, code, string.Empty);
        }

        // Prefer Models namespace over Services for DTOs
        var preferred = matchingTypes.FirstOrDefault(t => t.Namespace.Contains("Models"))
            ?? matchingTypes.First();

        var result = _baseFixer.ResolveAmbiguousReferences(code,
            new Dictionary<string, string> { { typeName, preferred.FullyQualifiedName } });

        if (result != code)
        {
            return (true, result, $"Fully qualified {typeName} to {preferred.FullyQualifiedName}");
        }

        return (false, code, string.Empty);
    }

    /// <summary>
    /// Fixes missing using directive.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixMissingUsing(
        string code, string message)
    {
        var match = Regex.Match(message, @"The type or namespace name '([^']+)' could not be found");
        
        if (!match.Success)
        {
            return (false, code, string.Empty);
        }

        var typeName = match.Groups[1].Value;

        // Try to find the namespace from registry or catalog
        var registryEntry = _typeRegistry.GetTypesBySimpleName(typeName).FirstOrDefault();
        if (registryEntry != null)
        {
            var newCode = _baseFixer.AddMissingUsings(code, new List<string> { registryEntry.Namespace });
            if (newCode != code)
            {
                return (true, newCode, $"Added using {registryEntry.Namespace}");
            }
        }

        // Check contract catalog
        if (_contractCatalog != null)
        {
            var contract = _contractCatalog.GetContract(typeName);
            if (contract != null)
            {
                var newCode = _baseFixer.AddMissingUsings(code, new List<string> { contract.Namespace });
                if (newCode != code)
                {
                    return (true, newCode, $"Added using {contract.Namespace}");
                }
            }
        }

        return (false, code, string.Empty);
    }

    /// <summary>
    /// Fixes missing namespace.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixMissingNamespace(
        string code, string message)
    {
        var match = Regex.Match(message, @"does not exist in the namespace '([^']+)'");
        
        if (!match.Success)
        {
            return (false, code, string.Empty);
        }

        var parentNs = match.Groups[1].Value;
        var newCode = _baseFixer.AddMissingUsings(code, new List<string> { parentNs });
        
        if (newCode != code)
        {
            return (true, newCode, $"Added using {parentNs}");
        }

        return (false, code, string.Empty);
    }

    /// <summary>
    /// Fixes duplicate type definition.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixDuplicateType(
        string code, string message, Location location)
    {
        var match = Regex.Match(message, @"already contains a definition for '([^']+)'");
        
        if (!match.Success)
        {
            return (false, code, string.Empty);
        }

        var typeName = match.Groups[1].Value;

        // Find and remove the duplicate type
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        var node = root.FindNode(location.SourceSpan);

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

    /// <summary>
    /// Fixes duplicate member definition.
    /// </summary>
    private (bool Applied, string NewCode, string Description) TryFixDuplicateMember(
        string code, string message, Location location)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        var node = root.FindNode(location.SourceSpan);

        if (node is MemberDeclarationSyntax memberDecl)
        {
            var newRoot = root.RemoveNode(memberDecl, SyntaxRemoveOptions.KeepNoTrivia);
            if (newRoot != null)
            {
                return (true, newRoot.ToFullString(), "Removed duplicate member");
            }
        }

        return (false, code, string.Empty);
    }

    /// <summary>
    /// Generates a method stub from a contract.
    /// </summary>
    private string GenerateMethodStub(MethodSignatureContract method, bool isOverride)
    {
        var sb = new StringBuilder();
        
        var modifiers = isOverride ? "public override" : "public";
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
        
        sb.AppendLine($"    {modifiers} {method.ReturnType} {method.Name}({parameters})");
        sb.AppendLine("    {");
        
        if (method.ReturnType == "void")
        {
            sb.AppendLine("        throw new NotImplementedException();");
        }
        else if (method.ReturnType.StartsWith("Task"))
        {
            if (method.ReturnType == "Task")
            {
                sb.AppendLine("        throw new NotImplementedException();");
            }
            else
            {
                sb.AppendLine("        throw new NotImplementedException();");
            }
        }
        else
        {
            sb.AppendLine("        throw new NotImplementedException();");
        }
        
        sb.AppendLine("    }");
        
        return sb.ToString();
    }

    /// <summary>
    /// Inserts a method into a class.
    /// </summary>
    private string InsertMethodIntoClass(string code, string className, string methodCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);

        if (classDecl == null)
        {
            return code;
        }

        // Parse the method and add it to the class
        var methodSyntax = SyntaxFactory.ParseMemberDeclaration(methodCode);
        if (methodSyntax == null)
        {
            return code;
        }

        var newMembers = classDecl.Members.Add(methodSyntax);
        var newClassDecl = classDecl.WithMembers(newMembers);
        var newRoot = root.ReplaceNode(classDecl, newClassDecl);

        return newRoot.ToFullString();
    }
}

/// <summary>
/// Result of auto-fix loop.
/// </summary>
public class AutoFixResult
{
    /// <summary>
    /// Whether all errors were fixed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Original code before fixes.
    /// </summary>
    public string OriginalCode { get; set; } = string.Empty;

    /// <summary>
    /// Code after fixes applied.
    /// </summary>
    public string FixedCode { get; set; } = string.Empty;

    /// <summary>
    /// List of fixes that were applied.
    /// </summary>
    public List<string> AppliedFixes { get; set; } = new();

    /// <summary>
    /// Errors that could not be fixed.
    /// </summary>
    public List<string> RemainingErrors { get; set; } = new();
}
