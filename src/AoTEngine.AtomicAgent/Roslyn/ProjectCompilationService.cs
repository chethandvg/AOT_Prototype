using AoTEngine.AtomicAgent.Blackboard;
using AoTEngine.AtomicAgent.Models;
using AoTEngine.AtomicAgent.Workspace;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AoTEngine.AtomicAgent.Roslyn;

/// <summary>
/// Handles project-level compilation for Progressive mode using dotnet build.
/// Compiles all generated files together using physical project structure and groups errors by atom.
/// </summary>
public class ProjectCompilationService
{
    private readonly BlackboardService _blackboard;
    private readonly WorkspaceService _workspace;
    private readonly ILogger<ProjectCompilationService> _logger;
    private readonly bool _suppressWarnings;
    private string? _solutionPath;

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
    /// Compiles all generated atoms using dotnet build on physical project structure.
    /// Returns grouped compilation results by atom.
    /// </summary>
    public async Task<ProjectCompilationResult> CompileProjectAsync(List<Atom> atoms, string solutionPath)
    {
        _logger.LogInformation("Compiling project with dotnet build using {Count} atoms", atoms.Count);
        _solutionPath = solutionPath;

        var result = new ProjectCompilationResult();

        // Restore NuGet packages first
        _logger.LogInformation("Restoring NuGet packages...");
        var restoreSuccess = await _workspace.RestorePackagesAsync(solutionPath);
        if (!restoreSuccess)
        {
            result.Success = false;
            result.ErrorsByAtom["_restore"] = new List<string> { "Package restore failed" };
            _logger.LogError("Package restore failed");
            return result;
        }

        // Build the solution
        _logger.LogInformation("Building solution: {SolutionPath}", solutionPath);
        var buildResult = await _workspace.BuildProjectAsync(solutionPath);
        
        result.Success = buildResult.Success;
        result.BuildOutput = buildResult.StandardOutput;
        result.BuildErrors = buildResult.ErrorOutput;

        if (buildResult.Success)
        {
            _logger.LogInformation("Build succeeded!");
            return result;
        }

        // Parse build errors and group by atom/file
        _logger.LogInformation("Build failed. Parsing errors...");
        var fileToAtomMap = atoms.ToDictionary(a => Path.GetFileName(a.FilePath), a => a.Id);
        
        ParseBuildErrors(buildResult.StandardOutput + "\n" + buildResult.ErrorOutput, fileToAtomMap, result);

        _logger.LogInformation(
            "Project build {Status}. Atoms with errors: {ErrorCount}/{Total}",
            result.Success ? "succeeded" : "failed",
            result.ErrorsByAtom.Count,
            atoms.Count);

        return result;
    }

    /// <summary>
    /// Parses build output and groups errors by atom/file.
    /// </summary>
    private void ParseBuildErrors(string buildOutput, Dictionary<string, string> fileToAtomMap, ProjectCompilationResult result)
    {
        // Regex pattern to match build errors: 
        // Example: "MyFile.cs(10,5): error CS0103: The name 'foo' does not exist"
        var errorPattern = @"(?<file>[^\(]+)\((?<line>\d+),(?<col>\d+)\):\s*(?<severity>error|warning)\s+(?<code>CS\d+):\s*(?<message>.+)";
        var regex = new Regex(errorPattern, RegexOptions.Multiline);
        
        var matches = regex.Matches(buildOutput);
        
        foreach (Match match in matches)
        {
            var severity = match.Groups["severity"].Value;
            
            // Skip warnings if suppressed
            if (_suppressWarnings && severity == "warning")
                continue;
                
            if (severity == "error")
            {
                var fileName = Path.GetFileName(match.Groups["file"].Value);
                var line = match.Groups["line"].Value;
                var code = match.Groups["code"].Value;
                var message = match.Groups["message"].Value;
                
                // Map file to atom
                var atomId = fileToAtomMap.ContainsKey(fileName) ? fileToAtomMap[fileName] : "Unknown";
                
                if (!result.ErrorsByAtom.ContainsKey(atomId))
                {
                    result.ErrorsByAtom[atomId] = new List<string>();
                }
                
                result.ErrorsByAtom[atomId].Add($"{code} (Line {line}): {message}");
            }
        }
        
        _logger.LogInformation("Parsed {Count} build errors from output", matches.Count);
    }

    /// <summary>
    /// Prioritizes atoms for fixing based on dependency order.
    /// Returns atoms with errors sorted so that dependencies are fixed first.
    /// </summary>
    public List<string> PrioritizeErroredAtoms(Dictionary<string, List<string>> errorsByAtom, List<Atom> allAtoms)
    {
        var erroredAtomIds = errorsByAtom.Keys.Where(id => id != "_restore").ToHashSet();
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
}

/// <summary>
/// Result of project-level compilation using dotnet build.
/// </summary>
public class ProjectCompilationResult
{
    public bool Success { get; set; }
    public Dictionary<string, List<string>> ErrorsByAtom { get; set; } = new();
    public string BuildOutput { get; set; } = string.Empty;
    public string BuildErrors { get; set; } = string.Empty;
    
    public int TotalErrors => ErrorsByAtom.Values.Sum(errors => errors.Count);
    public int AtomsWithErrors => ErrorsByAtom.Count;
}
