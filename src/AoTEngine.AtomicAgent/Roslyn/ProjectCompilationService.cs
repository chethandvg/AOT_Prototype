using AoTEngine.AtomicAgent.Blackboard;
using AoTEngine.AtomicAgent.Models;
using AoTEngine.AtomicAgent.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace AoTEngine.AtomicAgent.Roslyn;

/// <summary>
/// Handles project-level compilation for Progressive mode.
/// Compiles all generated files together and groups errors by atom.
/// </summary>
public class ProjectCompilationService
{
    private readonly BlackboardService _blackboard;
    private readonly WorkspaceService _workspace;
    private readonly ILogger<ProjectCompilationService> _logger;
    private readonly bool _suppressWarnings;

    public ProjectCompilationService(
        BlackboardService blackboard,
        WorkspaceService workspace,
        ILogger<ProjectCompilationService> logger,
        bool suppressWarnings = true)
    {
        _blackboard = blackboard;
        _workspace = workspace;
        _logger = logger;
        _suppressWarnings = suppressWarnings;
    }

    /// <summary>
    /// Compiles all generated atoms as a single project.
    /// Returns grouped compilation results by atom.
    /// </summary>
    public ProjectCompilationResult CompileProject(List<Atom> atoms)
    {
        _logger.LogInformation("Compiling project with {Count} atoms", atoms.Count);

        var result = new ProjectCompilationResult();
        var syntaxTrees = new List<SyntaxTree>();
        var fileToAtomMap = new Dictionary<string, string>();

        // Parse all atom codes into syntax trees
        foreach (var atom in atoms)
        {
            if (!string.IsNullOrWhiteSpace(atom.GeneratedCode))
            {
                try
                {
                    var syntaxTree = CSharpSyntaxTree.ParseText(
                        atom.GeneratedCode,
                        path: atom.FilePath);
                    
                    syntaxTrees.Add(syntaxTree);
                    fileToAtomMap[atom.FilePath] = atom.Id;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse atom {AtomId}", atom.Id);
                    result.ErrorsByAtom[atom.Id] = new List<string> { $"Parse error: {ex.Message}" };
                }
            }
        }

        if (syntaxTrees.Count == 0)
        {
            _logger.LogWarning("No valid syntax trees to compile");
            return result;
        }

        // Get system references
        var references = GetSystemReferences();

        // Create compilation
        var compilation = CSharpCompilation.Create(
            "GeneratedProject",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Emit to memory
        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        result.Success = emitResult.Success;

        // Group diagnostics by file/atom
        var diagnostics = emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error || !_suppressWarnings)
            .ToList();

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                var filePath = diagnostic.Location.SourceTree?.FilePath ?? "Unknown";
                var atomId = fileToAtomMap.ContainsKey(filePath) ? fileToAtomMap[filePath] : "Unknown";
                
                if (!result.ErrorsByAtom.ContainsKey(atomId))
                {
                    result.ErrorsByAtom[atomId] = new List<string>();
                }

                result.ErrorsByAtom[atomId].Add(FormatDiagnostic(diagnostic));
            }
        }

        _logger.LogInformation(
            "Project compilation {Status}. Atoms with errors: {ErrorCount}/{Total}",
            result.Success ? "succeeded" : "failed",
            result.ErrorsByAtom.Count,
            atoms.Count);

        return result;
    }

    /// <summary>
    /// Prioritizes atoms for fixing based on dependency order.
    /// Returns atoms with errors sorted so that dependencies are fixed first.
    /// </summary>
    public List<string> PrioritizeErroredAtoms(Dictionary<string, List<string>> errorsByAtom, List<Atom> allAtoms)
    {
        var erroredAtomIds = errorsByAtom.Keys.ToHashSet();
        var atomMap = allAtoms.ToDictionary(a => a.Id);
        var prioritized = new List<string>();
        var visited = new HashSet<string>();

        // Helper to perform DFS and add atoms in dependency order (leaf-first)
        void Visit(string atomId)
        {
            if (visited.Contains(atomId) || !atomMap.ContainsKey(atomId))
                return;

            visited.Add(atomId);

            // Visit dependencies first
            var atom = atomMap[atomId];
            foreach (var depId in atom.Dependencies)
            {
                if (erroredAtomIds.Contains(depId))
                {
                    Visit(depId);
                }
            }

            // Add this atom after its dependencies
            if (erroredAtomIds.Contains(atomId) && !prioritized.Contains(atomId))
            {
                prioritized.Add(atomId);
            }
        }

        // Process all errored atoms
        foreach (var atomId in erroredAtomIds)
        {
            Visit(atomId);
        }

        _logger.LogInformation(
            "Prioritized {Count} errored atoms for fixing (dependency-order)",
            prioritized.Count);

        return prioritized;
    }

    private List<MetadataReference> GetSystemReferences()
    {
        var references = new List<MetadataReference>();

        try
        {
            // Core runtime assemblies
            var runtimeAssemblies = new[]
            {
                typeof(object).Assembly,
                typeof(Console).Assembly,
                typeof(Enumerable).Assembly,
                Assembly.Load("System.Runtime"),
                Assembly.Load("System.Collections"),
                Assembly.Load("netstandard")
            };

            foreach (var assembly in runtimeAssemblies)
            {
                if (!string.IsNullOrWhiteSpace(assembly.Location))
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading system references");
        }

        return references;
    }

    private string FormatDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location.GetLineSpan();
        return $"{diagnostic.Id} (Line {location.StartLinePosition.Line + 1}): {diagnostic.GetMessage()}";
    }
}

/// <summary>
/// Result of project-level compilation.
/// </summary>
public class ProjectCompilationResult
{
    public bool Success { get; set; }
    public Dictionary<string, List<string>> ErrorsByAtom { get; set; } = new();
    
    public int TotalErrors => ErrorsByAtom.Values.Sum(errors => errors.Count);
    public int AtomsWithErrors => ErrorsByAtom.Count;
}
