using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Core;

/// <summary>
/// Engine for executing tasks in parallel based on their dependencies.
/// </summary>
public class ParallelExecutionEngine
{
    private readonly OpenAIService _openAIService;
    private readonly CodeValidatorService _validatorService;
    private readonly UserInteractionService _userInteractionService;
    private const int MaxRetries = 3;

    public ParallelExecutionEngine(
        OpenAIService openAIService, 
        CodeValidatorService validatorService,
        UserInteractionService userInteractionService)
    {
        _openAIService = openAIService;
        _validatorService = validatorService;
        _userInteractionService = userInteractionService;
    }

    /// <summary>
    /// Executes all tasks in the DAG, running independent tasks in parallel.
    /// </summary>
    public async Task<List<TaskNode>> ExecuteTasksAsync(List<TaskNode> tasks)
    {
        var taskDict = tasks.ToDictionary(t => t.Id, t => t);
        var completedTasks = new Dictionary<string, TaskNode>();

        while (completedTasks.Count < tasks.Count)
        {
            // Find tasks that are ready to execute (all dependencies completed)
            var readyTasks = tasks
                .Where(t => !t.IsCompleted && AreAllDependenciesMet(t, completedTasks))
                .ToList();

            if (!readyTasks.Any())
            {
                // Check for circular dependencies or other issues
                var incompleteTasks = tasks.Where(t => !t.IsCompleted).ToList();
                if (incompleteTasks.Any())
                {
                    throw new InvalidOperationException(
                        $"Deadlock detected. Tasks {string.Join(", ", incompleteTasks.Select(t => t.Id))} cannot be completed. " +
                        "Possible circular dependencies or missing tasks.");
                }
                break;
            }

            // Execute ready tasks in parallel
            var executionTasks = readyTasks.Select(task => ExecuteTaskWithValidationAsync(task, completedTasks));
            var results = await Task.WhenAll(executionTasks);

            // Mark completed tasks
            foreach (var task in results)
            {
                task.IsCompleted = true;
                completedTasks[task.Id] = task;
            }

            Console.WriteLine($"Completed {completedTasks.Count}/{tasks.Count} tasks");
        }

        return tasks;
    }

    /// <summary>
    /// Checks if all dependencies for a task are met.
    /// </summary>
    private bool AreAllDependenciesMet(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        return task.Dependencies.All(depId => 
            completedTasks.ContainsKey(depId) && completedTasks[depId].IsValidated);
    }

    /// <summary>
    /// Executes a single task with validation and retry logic.
    /// </summary>
    private async Task<TaskNode> ExecuteTaskWithValidationAsync(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        Console.WriteLine($"Executing task: {task.Id} - {task.Description}");

        // Handle uncertainties in the task before execution
        task = await _userInteractionService.HandleTaskUncertaintyAsync(task);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Generate code for the task
                if (string.IsNullOrEmpty(task.GeneratedCode))
                {
                    task.GeneratedCode = await _openAIService.GenerateCodeAsync(task, completedTasks);
                    Console.WriteLine($"Generated code for task {task.Id} (attempt {attempt + 1})");
                }

                // Validate the generated code
                var validationResult = await _validatorService.ValidateCodeAsync(task.GeneratedCode);
                
                // Also run linting
                var lintResult = _validatorService.LintCode(task.GeneratedCode);
                validationResult.Warnings.AddRange(lintResult.Warnings);

                if (validationResult.IsValid)
                {
                    task.IsValidated = true;
                    task.ValidationErrors.Clear();
                    Console.WriteLine($"Task {task.Id} validated successfully");
                    break;
                }
                else
                {
                    task.ValidationErrors = validationResult.Errors;
                    Console.WriteLine($"Task {task.Id} validation failed (attempt {attempt + 1}): {string.Join(", ", validationResult.Errors)}");

                    if (attempt < MaxRetries - 1)
                    {
                        // Re-prompt with errors
                        task.GeneratedCode = await _openAIService.RegenerateCodeWithErrorsAsync(task, validationResult);
                        task.RetryCount++;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Task {task.Id} failed validation after {MaxRetries} attempts. Errors: {string.Join(", ", validationResult.Errors)}");
                    }
                }
            }
            catch (Exception ex) when (attempt < MaxRetries - 1)
            {
                Console.WriteLine($"Error executing task {task.Id} (attempt {attempt + 1}): {ex.Message}");
                task.RetryCount++;
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        return task;
    }

    /// <summary>
    /// Builds a topologically sorted list of tasks for visualization.
    /// </summary>
    public List<TaskNode> TopologicalSort(List<TaskNode> tasks)
    {
        var sorted = new List<TaskNode>();
        var visited = new HashSet<string>();
        var taskDict = tasks.ToDictionary(t => t.Id, t => t);

        void Visit(TaskNode task)
        {
            if (visited.Contains(task.Id))
                return;

            visited.Add(task.Id);

            foreach (var depId in task.Dependencies)
            {
                if (taskDict.TryGetValue(depId, out var depTask))
                {
                    Visit(depTask);
                }
            }

            sorted.Add(task);
        }

        foreach (var task in tasks)
        {
            Visit(task);
        }

        return sorted;
    }
}
