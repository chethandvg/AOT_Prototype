using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Core;

/// <summary>
/// Engine for executing tasks in parallel based on their dependencies.
/// This is the main partial class containing core fields, constructor, and utility methods.
/// </summary>
/// <remarks>
/// This class is split into multiple partial class files for maintainability:
/// - ParallelExecutionEngine.cs (this file): Core fields, constructor, and complexity analysis
/// - ParallelExecutionEngine.BatchValidation.cs: Batch validation execution methods
/// - ParallelExecutionEngine.HybridValidation.cs: Hybrid validation execution methods
/// - ParallelExecutionEngine.TaskExecution.cs: Individual task execution methods
/// - ParallelExecutionEngine.ProblemIdentification.cs: Problem identification and task regeneration methods
/// - ParallelExecutionEngine.Utilities.cs: Code combination and utility methods
/// </remarks>
public partial class ParallelExecutionEngine
{
    private readonly OpenAIService _openAIService;
    private readonly CodeValidatorService _validatorService;
    private readonly UserInteractionService _userInteractionService;
    private readonly ProjectBuildService? _buildService;
    private readonly DocumentationService? _documentationService;
    private readonly TaskComplexityAnalyzer _complexityAnalyzer;
    private readonly CheckpointService? _checkpointService;
    private readonly string? _outputDirectory;
    private readonly bool _saveCheckpoints;
    private readonly int _checkpointFrequency;
    private const int MaxRetries = 3;
    private const int DefaultMaxLineThreshold = 300;
    
    // Track state for checkpointing
    private string _projectRequest = string.Empty;
    private string _projectDescription = string.Empty;

    public ParallelExecutionEngine(
        OpenAIService openAIService, 
        CodeValidatorService validatorService,
        UserInteractionService userInteractionService,
        ProjectBuildService? buildService = null,
        string? outputDirectory = null,
        DocumentationService? documentationService = null,
        bool saveCheckpoints = true,
        int checkpointFrequency = 1)
    {
        _openAIService = openAIService;
        _validatorService = validatorService;
        _userInteractionService = userInteractionService;
        _buildService = buildService;
        _outputDirectory = outputDirectory;
        _documentationService = documentationService;
        _complexityAnalyzer = new TaskComplexityAnalyzer();
        _saveCheckpoints = saveCheckpoints && !string.IsNullOrEmpty(outputDirectory);
        _checkpointFrequency = checkpointFrequency;
        _checkpointService = _saveCheckpoints ? new CheckpointService(outputDirectory) : null;
    }

    /// <summary>
    /// Performs pre-execution complexity analysis and decomposition of complex tasks.
    /// Ensures no task will generate more than the specified line threshold.
    /// </summary>
    /// <param name="tasks">List of tasks to analyze.</param>
    /// <param name="maxLineThreshold">Maximum lines per task (default: 300).</param>
    /// <returns>Modified task list with complex tasks decomposed.</returns>
    public async Task<List<TaskNode>> AnalyzeAndDecomposeComplexTasksAsync(
        List<TaskNode> tasks,
        int maxLineThreshold = DefaultMaxLineThreshold)
    {
        Console.WriteLine($"\n📊 Analyzing task complexity (max {maxLineThreshold} lines per task)...");

        var complexTasks = _complexityAnalyzer.AnalyzeTasksForDecomposition(tasks, maxLineThreshold);

        if (!complexTasks.Any())
        {
            Console.WriteLine("✅ All tasks are within complexity limits.");
            return tasks;
        }

        Console.WriteLine($"⚠️  Found {complexTasks.Count} task(s) requiring decomposition:");
        foreach (var (task, metrics) in complexTasks)
        {
            Console.WriteLine($"   - {task.Id}: ~{metrics.EstimatedLineCount} lines (score: {metrics.ComplexityScore}/100)");
        }

        var modifiedTasks = new List<TaskNode>(tasks);

        // Decompose each complex task
        foreach (var (task, metrics) in complexTasks)
        {
            try
            {
                Console.WriteLine($"\n📋 Decomposing '{task.Id}' into {metrics.RecommendedSubtaskCount} subtasks...");

                var subtasks = await _openAIService.DecomposeComplexTaskAsync(
                    task,
                    metrics.RecommendedSubtaskCount,
                    maxLineThreshold);

                if (subtasks.Any())
                {
                    // Replace original task with subtasks
                    modifiedTasks = ReplaceTaskWithSubtasks(modifiedTasks, task, subtasks);
                    Console.WriteLine($"✅ Decomposed '{task.Id}' into {subtasks.Count} subtasks");
                }
                else
                {
                    Console.WriteLine($"⚠️  Could not decompose '{task.Id}', keeping original");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to decompose '{task.Id}': {ex.Message}");
                // Keep original task if decomposition fails
            }
        }

        Console.WriteLine($"\n📋 Final task count: {modifiedTasks.Count}");
        return modifiedTasks;
    }

    /// <summary>
    /// Replaces a task with its subtasks in the task list.
    /// </summary>
    private List<TaskNode> ReplaceTaskWithSubtasks(
        List<TaskNode> tasks,
        TaskNode originalTask,
        List<TaskNode> subtasks)
    {
        var result = new List<TaskNode>();

        foreach (var task in tasks)
        {
            if (task.Id == originalTask.Id)
            {
                // Replace with subtasks
                result.AddRange(subtasks);
            }
            else
            {
                // Update dependencies that reference the original task
                if (task.Dependencies != null && task.Dependencies.Contains(originalTask.Id))
                {
                    task.Dependencies.Remove(originalTask.Id);
                    // Depend on the last subtask instead
                    if (subtasks.Any())
                    {
                        task.Dependencies.Add(subtasks.Last().Id);
                    }
                }
                result.Add(task);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets complexity metrics for a specific task.
    /// </summary>
    public ComplexityMetrics GetTaskComplexityMetrics(TaskNode task, int maxLineThreshold = DefaultMaxLineThreshold)
    {
        return _complexityAnalyzer.AnalyzeTask(task, maxLineThreshold);
    }

    /// <summary>
    /// Checks if all dependencies for a task have generated code (for batch validation mode).
    /// </summary>
    private bool AreAllDependenciesGenerated(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        return task.Dependencies.All(depId => completedTasks.ContainsKey(depId));
    }

    /// <summary>
    /// Checks if all dependencies for a task are met (for individual validation mode).
    /// </summary>
    private bool AreAllDependenciesMet(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        return task.Dependencies.All(depId => 
            completedTasks.TryGetValue(depId, out var completedTask) && completedTask.IsValidated);
    }

    /// <summary>
    /// Generates a summary for a task using the documentation service.
    /// </summary>
    private async Task GenerateTaskSummaryAsync(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        if (_documentationService == null)
        {
            return;
        }
        
        try
        {
            Console.WriteLine($"   📝 Generating summary for task {task.Id}...");
            await _documentationService.GenerateTaskSummaryAsync(task, completedTasks);
        }
        catch (Exception ex)
        {
            // Summary generation should not fail the task
            Console.WriteLine($"   ⚠️  Failed to generate summary for task {task.Id}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Generates summaries for all tasks after batch validation.
    /// </summary>
    private async Task GenerateSummariesForAllTasksAsync(List<TaskNode> tasks, Dictionary<string, TaskNode> completedTasks)
    {
        if (_documentationService == null)
        {
            return;
        }
        
        Console.WriteLine("\n📝 Generating summaries for all tasks...");
        
        foreach (var task in tasks)
        {
            await GenerateTaskSummaryAsync(task, completedTasks);
        }
        
        Console.WriteLine("✅ Summary generation complete.");
    }

    /// <summary>
    /// Sets the project context for checkpoint generation.
    /// </summary>
    public void SetProjectContext(string projectRequest, string projectDescription)
    {
        _projectRequest = projectRequest;
        _projectDescription = projectDescription;
    }

    /// <summary>
    /// Saves a checkpoint of the current execution state.
    /// </summary>
    private async Task SaveCheckpointAsync(
        List<TaskNode> tasks,
        Dictionary<string, TaskNode> completedTasks,
        string executionStatus = "in_progress")
    {
        if (_checkpointService == null || !_saveCheckpoints)
        {
            return;
        }

        // Only limit checkpointing frequency for non-final checkpoints
        // Always save final checkpoint regardless of frequency
        if (executionStatus != "completed" && 
            _checkpointFrequency > 1 && 
            completedTasks.Count % _checkpointFrequency != 0)
        {
            return;
        }

        var completedTaskIds = new HashSet<string>(completedTasks.Keys);
        await _checkpointService.SaveCheckpointAsync(
            tasks,
            completedTaskIds,
            _projectRequest,
            _projectDescription,
            executionStatus);
    }
}
