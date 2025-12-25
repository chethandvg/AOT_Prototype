using AoTEngine.Models;
using System.Text.RegularExpressions;

namespace AoTEngine.Services;

/// <summary>
/// Service for automatically decomposing complex tasks into smaller subtasks.
/// Part 2: Partial class strategy and subtask processing.
/// </summary>
public partial class AutoDecomposer
{
    /// <summary>
    /// Processes and validates subtasks from OpenAI.
    /// </summary>
    private List<TaskNode> ProcessAndValidateSubtasks(
        List<TaskNode> subtasks,
        TaskNode originalTask,
        ComplexityMetrics metrics)
    {
        var processedSubtasks = new List<TaskNode>();

        for (int i = 0; i < subtasks.Count; i++)
        {
            var subtask = subtasks[i];

            // Ensure proper ID format
            if (string.IsNullOrEmpty(subtask.Id))
            {
                subtask.Id = $"{originalTask.Id}_part{i + 1}";
            }

            // Inherit namespace if not specified
            if (string.IsNullOrEmpty(subtask.Namespace))
            {
                subtask.Namespace = originalTask.Namespace;
            }

            // Set up dependencies correctly
            if (i > 0 && !subtask.Dependencies.Contains(subtasks[i - 1].Id))
            {
                // Each part depends on previous part for partial classes
                subtask.Dependencies.Add(subtasks[i - 1].Id);
            }

            // Inherit original task's dependencies for first subtask
            if (i == 0 && originalTask.Dependencies.Any())
            {
                foreach (var dep in originalTask.Dependencies)
                {
                    if (!subtask.Dependencies.Contains(dep))
                    {
                        subtask.Dependencies.Add(dep);
                    }
                }
            }

            // Validate no circular dependencies
            if (HasCircularDependency(subtask, subtasks.Take(i).ToList()))
            {
                Console.WriteLine($"⚠️  Removing circular dependency in subtask '{subtask.Id}'");
                subtask.Dependencies = subtask.Dependencies
                    .Where(d => !subtasks.Take(i).Any(s => s.Id == d))
                    .ToList();
            }

            processedSubtasks.Add(subtask);
        }

        return processedSubtasks;
    }

    /// <summary>
    /// Checks for circular dependencies.
    /// </summary>
    private bool HasCircularDependency(TaskNode task, List<TaskNode> existingTasks)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        bool HasCycle(string taskId)
        {
            if (recursionStack.Contains(taskId))
                return true;
            if (visited.Contains(taskId))
                return false;

            visited.Add(taskId);
            recursionStack.Add(taskId);

            var taskToCheck = existingTasks.FirstOrDefault(t => t.Id == taskId);
            if (taskToCheck != null)
            {
                foreach (var dep in taskToCheck.Dependencies)
                {
                    if (HasCycle(dep))
                        return true;
                }
            }

            recursionStack.Remove(taskId);
            return false;
        }

        foreach (var dep in task.Dependencies)
        {
            if (dep == task.Id || HasCycle(dep))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates partial class configuration for the decomposition.
    /// </summary>
    private PartialClassConfig CreatePartialClassConfig(TaskNode originalTask, List<TaskNode> subtasks)
    {
        var config = new PartialClassConfig
        {
            Namespace = originalTask.Namespace,
            PartCount = subtasks.Count,
            NamingPattern = "{ClassName}.Part{PartNumber}"
        };

        // Extract base class name from expected types or description
        if (originalTask.ExpectedTypes?.Any() == true)
        {
            config.BaseClassName = originalTask.ExpectedTypes.First();
        }
        else
        {
            // Extract from description
            var classMatch = Regex.Match(
                originalTask.Description,
                @"class\s+(\w+)|implement\s+(\w+)|create\s+(\w+)Service",
                RegexOptions.IgnoreCase);
            
            if (classMatch.Success)
            {
                config.BaseClassName = classMatch.Groups
                    .Cast<Group>()
                    .Skip(1)
                    .FirstOrDefault(g => g.Success)?.Value ?? "GeneratedClass";
            }
            else
            {
                config.BaseClassName = "GeneratedClass";
            }
        }

        // Create method distributions
        for (int i = 0; i < subtasks.Count; i++)
        {
            var distribution = new MethodDistribution
            {
                PartNumber = i + 1,
                PartDescription = subtasks[i].Description
            };

            // Extract method names from expected types or description
            if (subtasks[i].ExpectedTypes?.Any() == true)
            {
                distribution.MethodNames.AddRange(subtasks[i].ExpectedTypes);
            }

            config.MethodDistributions.Add(distribution);
        }

        return config;
    }

    /// <summary>
    /// Identifies shared state across subtasks.
    /// </summary>
    private SharedStateInfo IdentifySharedState(TaskNode originalTask, List<TaskNode> subtasks)
    {
        var sharedState = new SharedStateInfo();

        // Analyze all subtasks for common patterns
        var allExpectedTypes = subtasks
            .Where(s => s.ExpectedTypes != null)
            .SelectMany(s => s.ExpectedTypes)
            .ToList();

        // Find interfaces (types starting with 'I' followed by uppercase)
        var interfaces = allExpectedTypes
            .Where(t => Regex.IsMatch(t, @"^I[A-Z]"))
            .Distinct()
            .ToList();

        sharedState.SharedInterfaces.AddRange(interfaces);

        // Common constructor parameters based on task description
        var lowerDesc = originalTask.Description.ToLowerInvariant();
        
        if (lowerDesc.Contains("logging") || lowerDesc.Contains("logger"))
        {
            sharedState.ConstructorParameters.Add("ILogger logger");
            sharedState.SharedFields.Add(new SharedField
            {
                Name = "_logger",
                Type = "ILogger",
                DefinedInPart = 1,
                IsReadOnly = true
            });
        }

        if (lowerDesc.Contains("configuration") || lowerDesc.Contains("options"))
        {
            sharedState.ConstructorParameters.Add("IConfiguration configuration");
            sharedState.SharedFields.Add(new SharedField
            {
                Name = "_configuration",
                Type = "IConfiguration",
                DefinedInPart = 1,
                IsReadOnly = true
            });
        }

        if (lowerDesc.Contains("http") || lowerDesc.Contains("client"))
        {
            sharedState.ConstructorParameters.Add("HttpClient httpClient");
            sharedState.SharedFields.Add(new SharedField
            {
                Name = "_httpClient",
                Type = "HttpClient",
                DefinedInPart = 1,
                IsReadOnly = true
            });
        }

        return sharedState;
    }

    /// <summary>
    /// Replaces a complex task with its subtasks in the task list.
    /// </summary>
    public List<TaskNode> ReplaceWithSubtasks(
        List<TaskNode> tasks,
        TaskNode originalTask,
        TaskDecompositionStrategy strategy)
    {
        if (!strategy.IsSuccessful || !strategy.Subtasks.Any())
        {
            return tasks;
        }

        var result = new List<TaskNode>();

        foreach (var task in tasks)
        {
            if (task.Id == originalTask.Id)
            {
                // Replace with subtasks
                result.AddRange(strategy.Subtasks);
            }
            else
            {
                // Update dependencies that reference the original task
                if (task.Dependencies.Contains(originalTask.Id))
                {
                    task.Dependencies.Remove(originalTask.Id);
                    // Depend on the last subtask instead
                    task.Dependencies.Add(strategy.Subtasks.Last().Id);
                }
                result.Add(task);
            }
        }

        return result;
    }
}
