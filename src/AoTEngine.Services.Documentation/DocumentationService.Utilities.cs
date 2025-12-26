using AoTEngine.Models;
using System.Security.Cryptography;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing utility methods for documentation generation.
/// </summary>
public partial class DocumentationService
{
    /// <summary>
    /// Creates a minimal summary record when LLM generation is disabled.
    /// </summary>
    private TaskSummaryRecord CreateMinimalSummaryRecord(TaskNode task)
    {
        return new TaskSummaryRecord
        {
            TaskId = task.Id,
            TaskDescription = task.Description,
            Dependencies = new List<string>(task.Dependencies),
            ExpectedTypes = new List<string>(task.ExpectedTypes),
            Namespace = task.Namespace,
            Purpose = $"Implements {task.Description}",
            ValidationNotes = BuildValidationNotes(task),
            GeneratedCode = task.GeneratedCode,
            GeneratedCodeHash = ComputeCodeHash(task.GeneratedCode),
            CreatedUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Builds validation notes from task state.
    /// </summary>
    private string BuildValidationNotes(TaskNode task)
    {
        var attemptCount = task.ValidationAttemptCount > 0 ? task.ValidationAttemptCount : task.RetryCount + 1;
        
        if (task.IsValidated)
        {
            if (attemptCount == 1)
            {
                return "Passed compilation on first attempt";
            }
            return $"Fixed after {attemptCount} attempt(s)";
        }
        
        if (task.ValidationErrors.Any())
        {
            return $"Validation issues: {string.Join("; ", task.ValidationErrors.Take(MaxValidationErrorsInNotes))}";
        }
        
        return "Pending validation";
    }

    /// <summary>
    /// Computes a hash of the generated code for tracking.
    /// </summary>
    private static string ComputeCodeHash(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        
        var bytes = Encoding.UTF8.GetBytes(code);
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash);
        var length = Math.Min(CodeHashLength, hex.Length);
        return hex[..length];
    }

    /// <summary>
    /// Builds a module index mapping types to their originating tasks.
    /// </summary>
    private Dictionary<string, string> BuildModuleIndex(List<TaskNode> tasks)
    {
        var index = tasks
            .SelectMany(task => task.ExpectedTypes.Select(type => new
            {
                Key = !string.IsNullOrEmpty(task.Namespace) 
                    ? $"{task.Namespace}.{type}" 
                    : type,
                TaskId = task.Id
            }))
            .ToDictionary(x => x.Key, x => x.TaskId);
        
        return index;
    }

    /// <summary>
    /// Builds a human-readable dependency graph summary.
    /// </summary>
    private string BuildDependencyGraphSummary(List<TaskNode> tasks)
    {
        var sb = new StringBuilder();
        
        // Group tasks by level (based on dependency depth)
        var levels = new Dictionary<int, List<TaskNode>>();
        var taskLevels = new Dictionary<string, int>();
        
        // Calculate levels
        foreach (var task in tasks)
        {
            var level = CalculateTaskLevel(task, tasks, taskLevels, new HashSet<string>());
            if (!levels.ContainsKey(level))
            {
                levels[level] = new List<TaskNode>();
            }
            levels[level].Add(task);
        }
        
        // Build graph representation
        foreach (var level in levels.OrderBy(l => l.Key))
        {
            sb.AppendLine($"Level {level.Key}:");
            foreach (var task in level.Value)
            {
                var deps = task.Dependencies.Any() 
                    ? $" → depends on: {string.Join(", ", task.Dependencies)}" 
                    : " (no dependencies)";
                sb.AppendLine($"  └─ {task.Id}: {task.Description}{deps}");
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Calculates the dependency level for a task.
    /// </summary>
    private int CalculateTaskLevel(TaskNode task, List<TaskNode> allTasks, Dictionary<string, int> cache, HashSet<string> visiting)
    {
        if (cache.TryGetValue(task.Id, out var cached))
        {
            return cached;
        }
        
        if (visiting.Contains(task.Id))
        {
            return 0; // Circular dependency, return 0
        }
        
        visiting.Add(task.Id);
        
        if (!task.Dependencies.Any())
        {
            cache[task.Id] = 0;
            visiting.Remove(task.Id);
            return 0;
        }
        
        var maxDepLevel = task.Dependencies
            .Select(depId => allTasks.FirstOrDefault(t => t.Id == depId))
            .Where(depTask => depTask != null)
            .Select(depTask => CalculateTaskLevel(depTask!, allTasks, cache, visiting))
            .DefaultIfEmpty(0)
            .Max();
        
        var level = maxDepLevel + 1;
        cache[task.Id] = level;
        visiting.Remove(task.Id);
        return level;
    }
}
