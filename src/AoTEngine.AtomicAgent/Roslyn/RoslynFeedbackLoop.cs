using AoTEngine.AtomicAgent.Blackboard;
using AoTEngine.AtomicAgent.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace AoTEngine.AtomicAgent.Roslyn;

/// <summary>
/// Implements the Roslyn Feedback Loop for in-memory compilation and semantic extraction.
/// Section 9 of the architectural blueprint.
/// </summary>
public class RoslynFeedbackLoop
{
    private readonly BlackboardService _blackboard;
    private readonly ILogger<RoslynFeedbackLoop> _logger;
    private readonly bool _suppressWarnings;

    public RoslynFeedbackLoop(
        BlackboardService blackboard,
        ILogger<RoslynFeedbackLoop> logger,
        bool suppressWarnings = true)
    {
        _blackboard = blackboard;
        _logger = logger;
        _suppressWarnings = suppressWarnings;
    }

    /// <summary>
    /// Compiles code in memory and returns diagnostics.
    /// </summary>
    public CompilationResult CompileInMemory(Atom atom)
    {
        _logger.LogInformation("Compiling atom {AtomId} ({Name}) in memory", atom.Id, atom.Name);

        var result = new CompilationResult { AtomId = atom.Id };

        try
        {
            // Parse the code into a syntax tree
            var syntaxTree = CSharpSyntaxTree.ParseText(atom.GeneratedCode);

            // Collect dependencies
            var dependencySyntaxTrees = new List<SyntaxTree>();
            foreach (var depId in atom.Dependencies)
            {
                var dep = _blackboard.GetAtom(depId);
                if (dep != null && !string.IsNullOrWhiteSpace(dep.GeneratedCode))
                {
                    dependencySyntaxTrees.Add(CSharpSyntaxTree.ParseText(dep.GeneratedCode));
                }
            }

            // Get system references
            var references = GetSystemReferences();

            // Create compilation
            var compilation = CSharpCompilation.Create(
                "DynamicAssembly_" + atom.Id,
                syntaxTrees: new[] { syntaxTree }.Concat(dependencySyntaxTrees),
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Emit to memory
            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            result.Success = emitResult.Success;

            // Capture diagnostics
            var diagnostics = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error || !_suppressWarnings)
                .ToList();

            result.Errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => FormatDiagnostic(d))
                .ToList();

            result.Warnings = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .Select(d => FormatDiagnostic(d))
                .ToList();

            if (result.Success)
            {
                _logger.LogInformation("✓ Compilation successful for {AtomId}", atom.Id);
                
                // Extract semantic information
                ExtractSemanticInfo(syntaxTree, atom);
            }
            else
            {
                _logger.LogWarning("✗ Compilation failed for {AtomId} with {ErrorCount} errors", 
                    atom.Id, result.Errors.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during compilation of {AtomId}", atom.Id);
            result.Success = false;
            result.Errors.Add($"Exception: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Gets system assembly references for compilation.
    /// </summary>
    private List<MetadataReference> GetSystemReferences()
    {
        var references = new List<MetadataReference>();

        // Add core .NET references
        var assemblies = new[]
        {
            typeof(object).Assembly,                    // System.Private.CoreLib
            typeof(Console).Assembly,                   // System.Console
            typeof(System.Linq.Enumerable).Assembly,   // System.Linq
        };

        foreach (var assembly in assemblies)
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load reference: {Assembly}", assembly.FullName);
            }
        }

        // Try to load additional references
        try
        {
            var runtimeAssembly = Assembly.Load("System.Runtime");
            references.Add(MetadataReference.CreateFromFile(runtimeAssembly.Location));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "System.Runtime assembly not loaded");
        }

        try
        {
            var netstandardAssembly = Assembly.Load("netstandard");
            references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "netstandard assembly not loaded");
        }

        return references;
    }

    /// <summary>
    /// Formats a diagnostic message for human readability.
    /// </summary>
    private string FormatDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location.GetLineSpan();
        return $"{diagnostic.Id} at Line {location.StartLinePosition.Line + 1}: {diagnostic.GetMessage()}";
    }

    /// <summary>
    /// Extracts semantic information (interfaces, DTOs) and updates the Symbol Table.
    /// Section 9.3 of the blueprint.
    /// </summary>
    private void ExtractSemanticInfo(SyntaxTree syntaxTree, Atom atom)
    {
        var root = syntaxTree.GetRoot();
        var namespaceName = ExtractNamespace(root);

        // Extract interface signatures
        var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        foreach (var iface in interfaces)
        {
            var signature = new InterfaceSignature
            {
                Name = iface.Identifier.Text,
                Namespace = namespaceName,
                Methods = ExtractMethodSignatures(iface)
            };

            _blackboard.AddInterfaceSignature(signature);
            _logger.LogDebug("Extracted interface: {Namespace}.{Name}", namespaceName, signature.Name);
        }

        // Extract DTO/class signatures
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var cls in classes)
        {
            if (atom.Type == AtomType.Dto || cls.Identifier.Text.EndsWith("Dto") || 
                cls.Identifier.Text.EndsWith("Model"))
            {
                var signature = new DtoSignature
                {
                    Name = cls.Identifier.Text,
                    Namespace = namespaceName,
                    Properties = ExtractPropertySignatures(cls)
                };

                _blackboard.AddDtoSignature(signature);
                _logger.LogDebug("Extracted DTO: {Namespace}.{Name}", namespaceName, signature.Name);
            }
        }
    }

    /// <summary>
    /// Extracts namespace from syntax tree.
    /// </summary>
    private string ExtractNamespace(SyntaxNode root)
    {
        var namespaceDecl = root.DescendantNodes()
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDecl != null)
        {
            return namespaceDecl.Name.ToString();
        }

        var fileScopedNamespace = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return fileScopedNamespace?.Name.ToString() ?? "Global";
    }

    /// <summary>
    /// Extracts method signatures from an interface.
    /// </summary>
    private List<string> ExtractMethodSignatures(InterfaceDeclarationSyntax iface)
    {
        var methods = new List<string>();

        foreach (var method in iface.Members.OfType<MethodDeclarationSyntax>())
        {
            var returnType = method.ReturnType.ToString();
            var methodName = method.Identifier.Text;
            var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => 
                $"{p.Type} {p.Identifier}"));

            methods.Add($"{returnType} {methodName}({parameters})");
        }

        return methods;
    }

    /// <summary>
    /// Extracts property signatures from a class.
    /// </summary>
    private List<string> ExtractPropertySignatures(ClassDeclarationSyntax cls)
    {
        var properties = new List<string>();

        foreach (var prop in cls.Members.OfType<PropertyDeclarationSyntax>())
        {
            var type = prop.Type.ToString();
            var name = prop.Identifier.Text;
            var accessors = prop.AccessorList?.Accessors.Select(a => a.Keyword.Text).ToList() 
                ?? new List<string>();

            var accessorString = accessors.Any() ? $" {{ {string.Join("; ", accessors)}; }}" : "";
            properties.Add($"public {type} {name}{accessorString}");
        }

        return properties;
    }
}

/// <summary>
/// Result of in-memory compilation.
/// </summary>
public class CompilationResult
{
    public string AtomId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
