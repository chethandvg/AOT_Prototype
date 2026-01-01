using AoTEngine.AtomicAgent.Blackboard;
using AoTEngine.AtomicAgent.ClarificationLoop;
using AoTEngine.AtomicAgent.Context;
using AoTEngine.AtomicAgent.Execution;
using AoTEngine.AtomicAgent.Models;
using AoTEngine.AtomicAgent.Planner;
using AoTEngine.AtomicAgent.Workspace;
using Microsoft.Extensions.Logging;

namespace AoTEngine.AtomicAgent;

/// <summary>
/// Main orchestrator implementing the 8-component Atomic Thought Framework.
/// Coordinates: Workspace, Blackboard, Planner, Clarification, Context, Execution, Roslyn, GC.
/// </summary>
public class AtomicAgentOrchestrator
{
    private readonly WorkspaceService _workspace;
    private readonly BlackboardService _blackboard;
    private readonly PlannerAgent _planner;
    private readonly ClarificationService _clarification;
    private readonly ContextEngine _contextEngine;
    private readonly AtomicWorkerAgent _worker;
    private readonly ILogger<AtomicAgentOrchestrator> _logger;

    public AtomicAgentOrchestrator(
        WorkspaceService workspace,
        BlackboardService blackboard,
        PlannerAgent planner,
        ClarificationService clarification,
        ContextEngine contextEngine,
        AtomicWorkerAgent worker,
        ILogger<AtomicAgentOrchestrator> logger)
    {
        _workspace = workspace;
        _blackboard = blackboard;
        _planner = planner;
        _clarification = clarification;
        _contextEngine = contextEngine;
        _worker = worker;
        _logger = logger;
    }

    /// <summary>
    /// Executes the complete Atomic Thought workflow.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(string userRequest)
    {
        var result = new ExecutionResult { OriginalRequest = userRequest };

        try
        {
            _logger.LogInformation("=== Starting Atomic Thought Framework Execution ===");

            // Phase 1: Clarification Loop (Section 6)
            Console.WriteLine("\n📋 Phase 1: Analyzing request for ambiguities...");
            var clarifiedRequest = await _clarification.AnalyzeAndClarifyAsync(userRequest);
            result.ClarifiedRequest = clarifiedRequest;

            // Phase 2: Planning with Abstractions First (Section 5)
            Console.WriteLine("\n🧠 Phase 2: Generating execution plan (Abstractions First)...");
            var atoms = await _planner.GeneratePlanAsync(clarifiedRequest);
            result.TotalAtoms = atoms.Count;

            // Store atoms in blackboard
            foreach (var atom in atoms)
            {
                // Set file path based on layer and type, using normalized casing and workspace-safe path
                var relativePath = Path.Combine("src", atom.Layer, $"{atom.Type.ToLowerInvariant()}s", $"{atom.Name}.cs");
                atom.FilePath = _workspace.GetSafePath(relativePath);
                
                _blackboard.UpsertAtom(atom);
            }

            // Fix architectural violations before validation
            var fixedCount = FixCoreLayerViolations(atoms);
            if (fixedCount > 0)
            {
                _logger.LogWarning("Fixed {Count} Core layer architectural violations by reassigning layers", fixedCount);
                Console.WriteLine($"   ⚠️  Fixed {fixedCount} Core layer atoms with dependencies by moving to Infrastructure layer");
            }

            // Validate architectural constraints after fixes
            foreach (var atom in atoms)
            {
                if (!_blackboard.ValidateLayerDependencies(atom))
                {
                    result.Success = false;
                    result.ErrorMessage = $"Architectural validation failed for atom {atom.Id}. See logs for details.";
                    return result;
                }
            }

            Console.WriteLine($"   Generated {atoms.Count} atoms:");
            foreach (var atom in atoms)
            {
                var deps = atom.Dependencies.Any() 
                    ? string.Join(", ", atom.Dependencies) 
                    : "None";
                Console.WriteLine($"   - {atom.Id} ({atom.Type}): {atom.Name} [deps: {deps}]");
            }

            // Phase 3: Execution Loop (Section 8)
            Console.WriteLine("\n⚙️  Phase 3: Executing atoms in dependency order...");
            
            var pendingAtoms = new Queue<Atom>(atoms);
            var executedCount = 0;
            var maxIterations = atoms.Count * 2; // Prevent infinite loops
            var iteration = 0;

            while (pendingAtoms.Count > 0 && iteration < maxIterations)
            {
                iteration++;
                var currentBatchSize = pendingAtoms.Count;
                var readyAtoms = new List<Atom>();

                // Find atoms whose dependencies are satisfied
                for (int i = 0; i < currentBatchSize; i++)
                {
                    var atom = pendingAtoms.Dequeue();
                    
                    if (_blackboard.AreDependenciesSatisfied(atom))
                    {
                        readyAtoms.Add(atom);
                    }
                    else
                    {
                        // Re-queue if dependencies not met
                        pendingAtoms.Enqueue(atom);
                    }
                }

                if (readyAtoms.Count == 0)
                {
                    _logger.LogError("Deadlock detected: No atoms ready but {Count} pending", 
                        pendingAtoms.Count);
                    result.Success = false;
                    result.ErrorMessage = "Dependency deadlock - circular dependencies may exist";
                    return result;
                }

                // Execute ready atoms (can be parallelized in future)
                foreach (var atom in readyAtoms)
                {
                    Console.WriteLine($"\n   → Executing {atom.Id}: {atom.Name} ({atom.Type})...");
                    
                    var success = await _worker.ExecuteAtomAsync(atom);
                    
                    if (success)
                    {
                        executedCount++;
                        result.CompletedAtoms++;
                        Console.WriteLine($"      ✓ Success (retry count: {atom.RetryCount})");
                        
                        // Write to workspace
                        await _workspace.WriteFileAsync(atom.FilePath, atom.GeneratedCode);
                    }
                    else
                    {
                        result.FailedAtoms++;
                        Console.WriteLine($"      ✗ Failed after {atom.RetryCount + 1} attempts");
                        
                        // Log errors
                        foreach (var error in atom.CompileErrors.Take(3))
                        {
                            Console.WriteLine($"         Error: {error}");
                        }
                    }
                }
            }

            // Phase 4: Report Results
            Console.WriteLine("\n📊 Phase 4: Execution Complete");
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine($"Total Atoms: {result.TotalAtoms}");
            Console.WriteLine($"Completed: {result.CompletedAtoms}");
            Console.WriteLine($"Failed: {result.FailedAtoms}");
            Console.WriteLine($"Workspace: {_workspace.RootPath}");
            Console.WriteLine();

            // Save final manifest
            _blackboard.SaveManifest();
            Console.WriteLine($"Solution manifest saved to: solution_manifest.json");

            result.Success = result.FailedAtoms == 0;
            result.WorkspacePath = _workspace.RootPath;

            if (result.Success)
            {
                Console.WriteLine("\n✅ All atoms completed successfully!");
            }
            else
            {
                Console.WriteLine($"\n⚠️  {result.FailedAtoms} atoms failed. Review errors above.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execution failed with exception");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.WriteLine($"\n❌ Execution failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Fixes Core layer architectural violations by moving atoms with dependencies to Infrastructure layer.
    /// Core layer atoms should have zero dependencies according to Clean Architecture.
    /// </summary>
    private int FixCoreLayerViolations(List<Atom> atoms)
    {
        int fixedCount = 0;

        foreach (var atom in atoms.Where(a => a.Layer == "Core" && a.Dependencies.Any()).ToList())
        {
            // Determine which dependencies are causing the issue
            var dependencyAtoms = atoms.Where(a => atom.Dependencies.Contains(a.Id)).ToList();
            
            // Check if all dependencies are also Core layer (which would be valid if they had no deps)
            bool allDepsAreCore = dependencyAtoms.All(d => d.Layer == "Core" && !d.Dependencies.Any());
            
            if (!allDepsAreCore)
            {
                // This atom has dependencies on non-Core or dependent Core atoms
                // Move it to Infrastructure layer
                _logger.LogWarning(
                    "Moving atom {AtomId} ({Name}) from Core to Infrastructure due to dependencies: {Dependencies}",
                    atom.Id, atom.Name, string.Join(", ", atom.Dependencies));
                
                atom.Layer = "Infrastructure";
                
                // Update file path for new layer
                var relativePath = Path.Combine("src", atom.Layer, $"{atom.Type.ToLowerInvariant()}s", $"{atom.Name}.cs");
                atom.FilePath = _workspace.GetSafePath(relativePath);
                
                // Update in blackboard
                _blackboard.UpsertAtom(atom);
                
                fixedCount++;
            }
        }

        return fixedCount;
    }
}

/// <summary>
/// Result of orchestrator execution.
/// </summary>
public class ExecutionResult
{
    public bool Success { get; set; }
    public string OriginalRequest { get; set; } = string.Empty;
    public string ClarifiedRequest { get; set; } = string.Empty;
    public int TotalAtoms { get; set; }
    public int CompletedAtoms { get; set; }
    public int FailedAtoms { get; set; }
    public string WorkspacePath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
