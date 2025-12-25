using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Partial class extending CodeMergerService with advanced integration capabilities.
/// Implements Parse → Analyze → Fix → Emit pipeline with type registry and deduplication.
/// </summary>
public partial class CodeMergerService
{
    private TypeRegistry? _typeRegistry;
    private SymbolTable? _symbolTable;
    private IntegrationFixer? _integrationFixer;

    /// <summary>
    /// Gets or creates the type registry for the current merge operation.
    /// </summary>
    public TypeRegistry TypeRegistry => _typeRegistry ??= new TypeRegistry();

    /// <summary>
    /// Gets or creates the symbol table for the current merge operation.
    /// </summary>
    public SymbolTable SymbolTable => _symbolTable ??= new SymbolTable();

    /// <summary>
    /// Gets or creates the integration fixer.
    /// </summary>
    private IntegrationFixer IntegrationFixer => 
        _integrationFixer ??= new IntegrationFixer(TypeRegistry, SymbolTable);

    /// <summary>
    /// Advanced merge operation that uses Parse → Analyze → Fix → Emit pipeline.
    /// </summary>
    /// <param name="tasks">Tasks with generated code to merge.</param>
    /// <param name="options">Options controlling the merge behavior.</param>
    /// <returns>The merged and fixed code.</returns>
    public async Task<MergeResult> MergeWithIntegrationAsync(List<TaskNode> tasks, MergeOptions? options = null)
    {
        options ??= new MergeOptions();
        var result = new MergeResult();

        try
        {
            // Reset registries for fresh merge
            TypeRegistry.Clear();
            SymbolTable.Clear();

            Console.WriteLine("\n🔧 Starting advanced code integration...");

            // Step 1: Parse all code snippets into syntax trees
            Console.WriteLine("   Step 1/4: Parsing code snippets...");
            var parsedTasks = ParseAllTasks(tasks);

            // Step 2: Build type registry and detect conflicts
            Console.WriteLine("   Step 2/4: Building type registry and detecting conflicts...");
            BuildTypeRegistry(parsedTasks);

            if (TypeRegistry.Conflicts.Any())
            {
                Console.WriteLine($"   ⚠️  Detected {TypeRegistry.Conflicts.Count} type conflict(s)");
                result.Conflicts.AddRange(TypeRegistry.Conflicts);

                // Handle conflicts based on options
                result = HandleConflicts(result, parsedTasks, options);
                if (!result.Success && options.FailOnUnresolvableConflicts)
                {
                    return result;
                }
            }

            // Step 3: Merge code with deduplication
            Console.WriteLine("   Step 3/4: Merging code with deduplication...");
            var mergedCode = MergeCodeWithDeduplication(parsedTasks, options);

            // Step 4: Compile and apply auto-fixes
            Console.WriteLine("   Step 4/4: Validating and applying auto-fixes...");
            var fixedResult = await ApplyIntegrationFixesAsync(mergedCode, options);
            
            result.MergedCode = fixedResult.FixedCode;
            result.AppliedFixes.AddRange(fixedResult.AppliedFixes);
            result.RemainingErrors.AddRange(fixedResult.UnfixableErrors);
            result.Success = !fixedResult.UnfixableErrors.Any();

            if (result.Success)
            {
                Console.WriteLine("   ✓ Advanced code integration completed successfully!");
            }
            else
            {
                Console.WriteLine($"   ⚠️  Integration completed with {result.RemainingErrors.Count} unfixable error(s)");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.RemainingErrors.Add($"Integration error: {ex.Message}");
            Console.WriteLine($"   ✗ Integration failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Parses all task code into syntax trees with metadata.
    /// </summary>
    private List<ParsedTaskCode> ParseAllTasks(List<TaskNode> tasks)
    {
        var result = new List<ParsedTaskCode>();

        foreach (var task in tasks.Where(t => !string.IsNullOrWhiteSpace(t.GeneratedCode)).OrderBy(t => t.Id))
        {
            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(task.GeneratedCode);
                var root = syntaxTree.GetRoot();

                var parsed = new ParsedTaskCode
                {
                    TaskId = task.Id,
                    OriginalCode = task.GeneratedCode,
                    SyntaxTree = syntaxTree,
                    Root = root
                };

                // Extract namespaces
                var namespaces = root.DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .Select(ns => ns.Name.ToString())
                    .ToList();
                parsed.Namespaces.AddRange(namespaces);

                // Extract usings
                var compilationUnit = root as CompilationUnitSyntax;
                if (compilationUnit != null)
                {
                    var usings = compilationUnit.Usings
                        .Select(u => u.ToString().Trim())
                        .ToList();
                    parsed.Usings.AddRange(usings);
                }

                // Extract types
                var typeDecls = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .ToList();
                parsed.TypeDeclarations.AddRange(typeDecls);

                result.Add(parsed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Failed to parse task {task.Id}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Builds the type registry from parsed tasks.
    /// </summary>
    private void BuildTypeRegistry(List<ParsedTaskCode> parsedTasks)
    {
        foreach (var parsed in parsedTasks)
        {
            foreach (var typeDecl in parsed.TypeDeclarations)
            {
                var entry = CreateTypeRegistryEntry(typeDecl, parsed.TaskId);
                TypeRegistry.TryRegister(entry);

                // Also register in symbol table
                var symbol = new ProjectSymbolInfo
                {
                    FullyQualifiedName = entry.FullyQualifiedName,
                    Namespace = entry.Namespace,
                    Name = entry.TypeName,
                    Kind = entry.Kind switch
                    {
                        ProjectTypeKind.Interface => ProjectSymbolKind.Interface,
                        ProjectTypeKind.Enum => ProjectSymbolKind.Enum,
                        _ => ProjectSymbolKind.Type
                    },
                    DefinedByTaskId = parsed.TaskId,
                    Signature = GetTypeSignature(typeDecl)
                };
                SymbolTable.TryRegister(symbol);
            }
        }
    }

    /// <summary>
    /// Creates a type registry entry from a type declaration.
    /// </summary>
    private TypeRegistryEntry CreateTypeRegistryEntry(TypeDeclarationSyntax typeDecl, string taskId)
    {
        var ns = GetNamespace(typeDecl);
        var typeName = typeDecl.Identifier.Text;

        var entry = new TypeRegistryEntry
        {
            TypeName = typeName,
            Namespace = ns ?? string.Empty,
            FullyQualifiedName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}",
            OwnerTaskId = taskId,
            IsPartial = typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword),
            Kind = typeDecl switch
            {
                InterfaceDeclarationSyntax => ProjectTypeKind.Interface,
                RecordDeclarationSyntax => ProjectTypeKind.Record,
                StructDeclarationSyntax => ProjectTypeKind.Struct,
                ClassDeclarationSyntax => ProjectTypeKind.Class,
                _ => ProjectTypeKind.Class
            },
            SyntaxNode = typeDecl
        };

        // Extract member signatures
        entry.Members = ExtractMemberSignatures(typeDecl);

        return entry;
    }

    /// <summary>
    /// Extracts member signatures from a type declaration.
    /// </summary>
    private List<MemberSignature> ExtractMemberSignatures(TypeDeclarationSyntax typeDecl)
    {
        var members = new List<MemberSignature>();

        foreach (var member in typeDecl.Members)
        {
            var sig = member switch
            {
                ConstructorDeclarationSyntax ctor => new MemberSignature
                {
                    Name = ctor.Identifier.Text,
                    Kind = ProjectMemberKind.Constructor,
                    ParameterTypes = ctor.ParameterList.Parameters
                        .Select(p => p.Type?.ToString() ?? string.Empty)
                        .ToList()
                },
                MethodDeclarationSyntax method => new MemberSignature
                {
                    Name = method.Identifier.Text,
                    Kind = ProjectMemberKind.Method,
                    ParameterTypes = method.ParameterList.Parameters
                        .Select(p => p.Type?.ToString() ?? string.Empty)
                        .ToList(),
                    ReturnType = method.ReturnType.ToString()
                },
                PropertyDeclarationSyntax prop => new MemberSignature
                {
                    Name = prop.Identifier.Text,
                    Kind = ProjectMemberKind.Property,
                    ReturnType = prop.Type.ToString()
                },
                FieldDeclarationSyntax field => field.Declaration.Variables
                    .Select(v => new MemberSignature
                    {
                        Name = v.Identifier.Text,
                        Kind = ProjectMemberKind.Field,
                        ReturnType = field.Declaration.Type.ToString()
                    }).FirstOrDefault(),
                _ => null
            };

            if (sig != null)
                members.Add(sig);
        }

        return members;
    }

    /// <summary>
    /// Handles type conflicts based on resolution options.
    /// </summary>
    private MergeResult HandleConflicts(MergeResult result, List<ParsedTaskCode> parsedTasks, MergeOptions options)
    {
        foreach (var conflict in TypeRegistry.Conflicts)
        {
            switch (conflict.SuggestedResolution)
            {
                case ConflictResolution.KeepFirst:
                    if (options.AutoResolveKeepFirst)
                    {
                        result.AppliedFixes.Add($"Kept first definition of {conflict.FullyQualifiedName} from task {conflict.ExistingEntry.OwnerTaskId}");
                        // Mark the duplicate for removal
                        MarkTypeForRemoval(parsedTasks, conflict.NewEntry);
                    }
                    else
                    {
                        result.ManualInterventionRequired.Add(conflict);
                    }
                    break;

                case ConflictResolution.MergeAsPartial:
                    if (options.EnablePartialClassMerge)
                    {
                        result.AppliedFixes.Add($"Merged {conflict.FullyQualifiedName} as partial class");
                        // Mark both for partial conversion
                        MarkForPartialConversion(parsedTasks, conflict.ExistingEntry, conflict.NewEntry);
                    }
                    else
                    {
                        result.ManualInterventionRequired.Add(conflict);
                    }
                    break;

                case ConflictResolution.RemoveDuplicate:
                    result.AppliedFixes.Add($"Removed duplicate members from {conflict.FullyQualifiedName}");
                    MarkDuplicateMembersForRemoval(parsedTasks, conflict);
                    break;

                case ConflictResolution.FailFast:
                    result.Success = false;
                    result.RemainingErrors.Add($"Unresolvable conflict: {conflict.FullyQualifiedName} defined in tasks {conflict.ExistingEntry.OwnerTaskId} and {conflict.NewEntry.OwnerTaskId}");
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Merges code with deduplication applied.
    /// </summary>
    private string MergeCodeWithDeduplication(List<ParsedTaskCode> parsedTasks, MergeOptions options)
    {
        var mergedCode = new StringBuilder();
        var allUsings = new HashSet<string>();
        var contentByNamespace = new Dictionary<string, StringBuilder>();

        foreach (var parsed in parsedTasks)
        {
            // Collect usings
            foreach (var usingStr in parsed.Usings)
            {
                allUsings.Add(usingStr);
            }

            // Organize content by namespace
            var compilationUnit = parsed.Root as CompilationUnitSyntax;
            if (compilationUnit == null) continue;

            foreach (var nsDecl in compilationUnit.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            {
                var nsName = nsDecl.Name.ToString();
                if (!contentByNamespace.ContainsKey(nsName))
                {
                    contentByNamespace[nsName] = new StringBuilder();
                }

                // Add members, excluding those marked for removal
                foreach (var member in nsDecl.Members)
                {
                    if (!ShouldRemove(member, parsed.TaskId))
                    {
                        var memberCode = ApplyPartialConversionIfNeeded(member, parsed.TaskId);
                        contentByNamespace[nsName].AppendLine(memberCode);
                    }
                }
            }

            // Handle global members (outside namespaces)
            var globalMembers = compilationUnit.Members
                .Where(m => m is not BaseNamespaceDeclarationSyntax)
                .ToList();
            
            if (globalMembers.Any())
            {
                if (!contentByNamespace.ContainsKey("Global"))
                {
                    contentByNamespace["Global"] = new StringBuilder();
                }
                foreach (var member in globalMembers)
                {
                    if (!ShouldRemove(member, parsed.TaskId))
                    {
                        contentByNamespace["Global"].AppendLine(member.ToFullString());
                    }
                }
            }
        }

        // Build final code
        // Add deduplicated usings
        foreach (var usingStr in allUsings.OrderBy(u => u))
        {
            mergedCode.AppendLine(usingStr);
        }
        mergedCode.AppendLine();

        // Add namespaced content
        foreach (var (ns, content) in contentByNamespace.OrderBy(kvp => kvp.Key))
        {
            if (ns == "Global")
            {
                mergedCode.Append(content);
            }
            else
            {
                mergedCode.AppendLine($"namespace {ns}");
                mergedCode.AppendLine("{");
                mergedCode.Append(content);
                mergedCode.AppendLine("}");
            }
        }

        return mergedCode.ToString();
    }

    /// <summary>
    /// Applies integration fixes using Roslyn compilation.
    /// </summary>
    private async Task<IntegrationFixResult> ApplyIntegrationFixesAsync(string code, MergeOptions options)
    {
        return await Task.Run(() =>
        {
            // Compile and get diagnostics
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var references = _validatorService.GetDefaultReferences();
            
            var compilation = CSharpCompilation.Create(
                $"IntegrationCheck_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var diagnostics = compilation.GetDiagnostics();

            if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return new IntegrationFixResult
                {
                    Success = true,
                    FixedCode = code
                };
            }

            // Apply auto-fixes if enabled
            if (options.EnableAutoFix)
            {
                return IntegrationFixer.TryFix(code, diagnostics);
            }

            return new IntegrationFixResult
            {
                Success = false,
                FixedCode = code,
                UnfixableErrors = diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToList()
            };
        });
    }

    // Tracking sets for removal and conversion
    private readonly HashSet<(string TaskId, string TypeName)> _typesToRemove = new();
    private readonly HashSet<(string TaskId, string TypeName)> _typesToConvertToPartial = new();
    private readonly Dictionary<(string TaskId, string TypeName), List<MemberSignature>> _membersToRemove = new();

    private void MarkTypeForRemoval(List<ParsedTaskCode> parsedTasks, TypeRegistryEntry entry)
    {
        _typesToRemove.Add((entry.OwnerTaskId, entry.TypeName));
    }

    private void MarkForPartialConversion(List<ParsedTaskCode> parsedTasks, TypeRegistryEntry existing, TypeRegistryEntry newEntry)
    {
        _typesToConvertToPartial.Add((existing.OwnerTaskId, existing.TypeName));
        _typesToConvertToPartial.Add((newEntry.OwnerTaskId, newEntry.TypeName));
    }

    private void MarkDuplicateMembersForRemoval(List<ParsedTaskCode> parsedTasks, TypeConflict conflict)
    {
        var key = (conflict.NewEntry.OwnerTaskId, conflict.NewEntry.TypeName);
        if (!_membersToRemove.ContainsKey(key))
        {
            _membersToRemove[key] = new List<MemberSignature>();
        }
        _membersToRemove[key].AddRange(conflict.ConflictingMembers);
    }

    private bool ShouldRemove(MemberDeclarationSyntax member, string taskId)
    {
        if (member is TypeDeclarationSyntax typeDecl)
        {
            return _typesToRemove.Contains((taskId, typeDecl.Identifier.Text));
        }
        return false;
    }

    private string ApplyPartialConversionIfNeeded(MemberDeclarationSyntax member, string taskId)
    {
        if (member is ClassDeclarationSyntax classDecl)
        {
            if (_typesToConvertToPartial.Contains((taskId, classDecl.Identifier.Text)))
            {
                if (!classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                        .WithTrailingTrivia(SyntaxFactory.Space);
                    var newModifiers = classDecl.Modifiers.Add(partialToken);
                    var newClassDecl = classDecl.WithModifiers(newModifiers);
                    return newClassDecl.ToFullString();
                }
            }
        }
        return member.ToFullString();
    }

    private string? GetNamespace(SyntaxNode node)
    {
        var nsDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return nsDecl?.Name.ToString();
    }

    private string GetTypeSignature(TypeDeclarationSyntax typeDecl)
    {
        var modifiers = string.Join(" ", typeDecl.Modifiers.Select(m => m.Text));
        var keyword = typeDecl.Keyword.Text;
        var name = typeDecl.Identifier.Text;
        var baseList = typeDecl.BaseList?.ToString() ?? string.Empty;
        return $"{modifiers} {keyword} {name}{baseList}".Trim();
    }

    /// <summary>
    /// Clears tracking state for a new merge operation.
    /// </summary>
    public void ResetMergeState()
    {
        _typeRegistry?.Clear();
        _symbolTable?.Clear();
        _typesToRemove.Clear();
        _typesToConvertToPartial.Clear();
        _membersToRemove.Clear();
    }
}

/// <summary>
/// Parsed task code with extracted metadata.
/// </summary>
public class ParsedTaskCode
{
    public string TaskId { get; set; } = string.Empty;
    public string OriginalCode { get; set; } = string.Empty;
    public SyntaxTree SyntaxTree { get; set; } = null!;
    public SyntaxNode Root { get; set; } = null!;
    public List<string> Namespaces { get; set; } = new();
    public List<string> Usings { get; set; } = new();
    public List<TypeDeclarationSyntax> TypeDeclarations { get; set; } = new();
}

/// <summary>
/// Options for controlling merge behavior.
/// </summary>
public class MergeOptions
{
    /// <summary>
    /// Whether to automatically keep the first definition for duplicates.
    /// </summary>
    public bool AutoResolveKeepFirst { get; set; } = true;

    /// <summary>
    /// Whether to enable partial class merging for compatible duplicates.
    /// </summary>
    public bool EnablePartialClassMerge { get; set; } = true;

    /// <summary>
    /// Whether to enable automatic fixing of integration errors.
    /// </summary>
    public bool EnableAutoFix { get; set; } = true;

    /// <summary>
    /// Whether to fail if conflicts cannot be resolved.
    /// </summary>
    public bool FailOnUnresolvableConflicts { get; set; } = false;

    /// <summary>
    /// Maximum number of auto-fix iterations.
    /// </summary>
    public int MaxAutoFixIterations { get; set; } = 3;
}

/// <summary>
/// Result of a merge operation with integration.
/// </summary>
public class MergeResult
{
    /// <summary>
    /// Whether the merge was successful.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// The merged code.
    /// </summary>
    public string MergedCode { get; set; } = string.Empty;

    /// <summary>
    /// Fixes that were applied during integration.
    /// </summary>
    public List<string> AppliedFixes { get; set; } = new();

    /// <summary>
    /// Errors that could not be fixed.
    /// </summary>
    public List<string> RemainingErrors { get; set; } = new();

    /// <summary>
    /// Detected type conflicts.
    /// </summary>
    public List<TypeConflict> Conflicts { get; set; } = new();

    /// <summary>
    /// Conflicts requiring manual intervention.
    /// </summary>
    public List<TypeConflict> ManualInterventionRequired { get; set; } = new();
}
