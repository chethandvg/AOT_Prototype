using AoTEngine.Models;

namespace AoTEngine.Core;

/// <summary>
/// Enhanced dependency graph manager with cycle detection and failure policies.
/// </summary>
public class DependencyGraphManagerV2
{
    private readonly Dictionary<string, List<string>> _dependencies = new();
    private readonly Dictionary<string, List<string>> _dependents = new();
    private readonly Dictionary<string, TaskStatus> _taskStatus = new();
    private readonly FailurePolicy _failurePolicy;
    private readonly HashSet<string> _completedTasks = new();
    private readonly Dictionary<string, DateTime> _completionTimes = new();
    private readonly TimeSpan _cleanupAge;

    public DependencyGraphManagerV2(
        FailurePolicy failurePolicy = FailurePolicy.Block,
        TimeSpan? cleanupAge = null)
    {
        _failurePolicy = failurePolicy;
        _cleanupAge = cleanupAge ?? TimeSpan.FromHours(24);
    }

    private enum TaskStatus
    {
        Pending,
        Ready,
        Running,
        Completed,
        Failed
    }

    /// <summary>
    /// Adds a dependency with cycle detection.
    /// </summary>
    public RegistrationResult AddDependency(string taskId, string dependsOn)
    {
        // Check for cycle
        if (WouldCreateCycle(taskId, dependsOn))
        {
            var cycle = FindCyclePath(taskId, dependsOn);
            return new RegistrationResult
            {
                Success = false,
                CycleDetected = true,
                CyclePath = cycle,
                ErrorMessage = $"Adding dependency would create cycle: {string.Join(" -> ", cycle)}"
            };
        }

        // Validate phantom dependency
        if (!_taskStatus.ContainsKey(dependsOn) && _failurePolicy == FailurePolicy.FailFast)
        {
            return new RegistrationResult
            {
                Success = false,
                ErrorMessage = $"Dependency {dependsOn} does not exist (phantom dependency)"
            };
        }

        // Add dependency
        if (!_dependencies.ContainsKey(taskId))
        {
            _dependencies[taskId] = new List<string>();
        }

        if (!_dependencies[taskId].Contains(dependsOn))
        {
            _dependencies[taskId].Add(dependsOn);
        }

        // Add to dependents
        if (!_dependents.ContainsKey(dependsOn))
        {
            _dependents[dependsOn] = new List<string>();
        }

        if (!_dependents[dependsOn].Contains(taskId))
        {
            _dependents[dependsOn].Add(taskId);
        }

        return new RegistrationResult { Success = true };
    }

    /// <summary>
    /// Registers a task in the graph.
    /// </summary>
    public void RegisterTask(string taskId)
    {
        if (!_taskStatus.ContainsKey(taskId))
        {
            _taskStatus[taskId] = TaskStatus.Pending;
        }
    }

    /// <summary>
    /// Marks a task as completed.
    /// </summary>
    public void MarkCompleted(string taskId)
    {
        _taskStatus[taskId] = TaskStatus.Completed;
        _completedTasks.Add(taskId);
        _completionTimes[taskId] = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks a task as failed.
    /// </summary>
    public void MarkFailed(string taskId)
    {
        _taskStatus[taskId] = TaskStatus.Failed;

        // Apply failure policy to dependents
        if (_dependents.TryGetValue(taskId, out var dependents))
        {
            foreach (var dependent in dependents)
            {
                if (_failurePolicy == FailurePolicy.FailFast)
                {
                    _taskStatus[dependent] = TaskStatus.Failed;
                }
                else if (_failurePolicy == FailurePolicy.SkipFailed)
                {
                    // Mark as failed but allow other paths to continue
                    _taskStatus[dependent] = TaskStatus.Failed;
                }
            }
        }
    }

    /// <summary>
    /// Gets tasks ready for execution (all dependencies completed).
    /// </summary>
    public List<string> GetReadyTasks()
    {
        var ready = new List<string>();

        foreach (var kvp in _taskStatus)
        {
            if (kvp.Value != TaskStatus.Pending && kvp.Value != TaskStatus.Ready)
            {
                continue;
            }

            var taskId = kvp.Key;
            if (!_dependencies.TryGetValue(taskId, out var deps))
            {
                deps = new List<string>();
            }

            // Check if all dependencies are completed
            var allDepsCompleted = deps.All(d =>
            {
                if (!_taskStatus.TryGetValue(d, out var status))
                {
                    return _failurePolicy == FailurePolicy.SkipMissing;
                }

                if (status == TaskStatus.Failed)
                {
                    return _failurePolicy == FailurePolicy.SkipFailed;
                }

                return status == TaskStatus.Completed;
            });

            if (allDepsCompleted)
            {
                ready.Add(taskId);
            }
        }

        return ready;
    }

    /// <summary>
    /// Calculates critical path through the graph.
    /// </summary>
    public List<string> CalculateCriticalPath()
    {
        var criticalPath = new List<string>();
        var maxDepth = new Dictionary<string, int>();

        // Calculate depth for each task
        foreach (var taskId in _taskStatus.Keys)
        {
            maxDepth[taskId] = CalculateDepth(taskId, maxDepth);
        }

        // Find tasks on critical path
        var maxDepthValue = maxDepth.Values.DefaultIfEmpty(0).Max();
        criticalPath = maxDepth.Where(kvp => kvp.Value == maxDepthValue)
            .Select(kvp => kvp.Key)
            .ToList();

        return criticalPath;
    }

    /// <summary>
    /// Cleans up completed tasks older than the configured age.
    /// </summary>
    public int CleanupOldTasks()
    {
        var cutoffTime = DateTime.UtcNow - _cleanupAge;
        var toRemove = _completionTimes
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var taskId in toRemove)
        {
            _completedTasks.Remove(taskId);
            _completionTimes.Remove(taskId);
            _taskStatus.Remove(taskId);
            _dependencies.Remove(taskId);
            _dependents.Remove(taskId);
        }

        return toRemove.Count;
    }

    /// <summary>
    /// Generates an execution plan with waves.
    /// </summary>
    public ExecutionPlan GenerateExecutionPlan()
    {
        var plan = new ExecutionPlan();
        var remaining = new HashSet<string>(_taskStatus.Keys);
        var waveNumber = 0;

        while (remaining.Count > 0)
        {
            var wave = new ExecutionWave { WaveNumber = waveNumber };

            // Find tasks with no remaining dependencies
            var waveTasks = remaining.Where(taskId =>
            {
                if (!_dependencies.TryGetValue(taskId, out var deps))
                {
                    return true; // No dependencies
                }
                return deps.All(d => !remaining.Contains(d));
            }).ToList();

            if (waveTasks.Count == 0 && remaining.Count > 0)
            {
                // Circular dependency or error
                break;
            }

            wave.TaskIds = waveTasks;
            wave.EstimatedDuration = TimeSpan.FromMinutes(waveTasks.Count); // Placeholder
            plan.Waves.Add(wave);

            foreach (var taskId in waveTasks)
            {
                remaining.Remove(taskId);
            }

            waveNumber++;
        }

        plan.CriticalPath = CalculateCriticalPath();
        plan.EstimatedDuration = TimeSpan.FromMinutes(plan.Waves.Sum(w => w.EstimatedDuration.TotalMinutes));

        return plan;
    }

    private bool WouldCreateCycle(string from, string to)
    {
        var visited = new HashSet<string>();
        return HasPath(to, from, visited);
    }

    private bool HasPath(string from, string to, HashSet<string> visited)
    {
        if (from == to) return true;
        if (visited.Contains(from)) return false;

        visited.Add(from);

        if (_dependencies.TryGetValue(from, out var deps))
        {
            foreach (var dep in deps)
            {
                if (HasPath(dep, to, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private List<string> FindCyclePath(string from, string to)
    {
        // We are considering adding an edge from -> to. A cycle exists if there is already
        // a path from 'to' back to 'from'. We find that path using DFS and then prepend
        // 'from' to build the full cycle chain for debugging.
        var visited = new HashSet<string>();
        var path = new List<string>();

        if (TryFindPath(to, from, visited, path) && path.Count > 0)
        {
            // 'path' currently contains a sequence starting at 'to' and ending at 'from'.
            // Prepend 'from' to show the full cycle: from -> to -> ... -> from
            path.Insert(0, from);
            return path;
        }

        // Fallback to the simplified two-node path if, for any reason, a full path
        // cannot be constructed.
        return new List<string> { from, to };
    }

    private bool TryFindPath(string current, string target, HashSet<string> visited, List<string> path)
    {
        if (visited.Contains(current))
        {
            return false;
        }

        visited.Add(current);
        path.Add(current);

        if (current == target)
        {
            return true;
        }

        if (_dependencies.TryGetValue(current, out var deps))
        {
            foreach (var dep in deps)
            {
                if (TryFindPath(dep, target, visited, path))
                {
                    return true;
                }
            }
        }

        // Backtrack if no path from 'current' to 'target' was found through its dependencies
        path.RemoveAt(path.Count - 1);
        return false;
    }

    private int CalculateDepth(string taskId, Dictionary<string, int> memo)
    {
        if (memo.TryGetValue(taskId, out var cachedDepth))
        {
            return cachedDepth;
        }

        if (!_dependencies.TryGetValue(taskId, out var deps) || deps.Count == 0)
        {
            memo[taskId] = 0;
            return 0;
        }

        var maxDepth = 0;
        foreach (var dep in deps)
        {
            var depth = CalculateDepth(dep, memo);
            maxDepth = Math.Max(maxDepth, depth);
        }

        memo[taskId] = maxDepth + 1;
        return maxDepth + 1;
    }
}
