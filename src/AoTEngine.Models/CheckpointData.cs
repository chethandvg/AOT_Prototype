namespace AoTEngine.Models;

/// <summary>
/// Represents a checkpoint snapshot of the execution state.
/// </summary>
public class CheckpointData
{
    /// <summary>
    /// UTC timestamp when the checkpoint was created.
    /// </summary>
    public DateTime CheckpointTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The original user request.
    /// </summary>
    public string ProjectRequest { get; set; } = string.Empty;

    /// <summary>
    /// Description of the decomposition.
    /// </summary>
    public string ProjectDescription { get; set; } = string.Empty;

    /// <summary>
    /// Total number of tasks in the project.
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    /// Number of completed tasks.
    /// </summary>
    public int CompletedTasks { get; set; }

    /// <summary>
    /// Number of pending tasks.
    /// </summary>
    public int PendingTasks { get; set; }

    /// <summary>
    /// Number of failed tasks.
    /// </summary>
    public int FailedTasks { get; set; }

    /// <summary>
    /// Details of all completed tasks.
    /// </summary>
    public List<CompletedTaskDetail> CompletedTaskDetails { get; set; } = new();

    /// <summary>
    /// IDs of pending tasks.
    /// </summary>
    public List<string> PendingTaskIds { get; set; } = new();

    /// <summary>
    /// IDs of failed tasks (if any).
    /// </summary>
    public List<string> FailedTaskIds { get; set; } = new();

    /// <summary>
    /// Dependency graph showing relationships between tasks.
    /// Key: task ID, Value: list of dependency task IDs.
    /// </summary>
    public Dictionary<string, List<string>> DependencyGraph { get; set; } = new();

    /// <summary>
    /// Architecture summary based on completed tasks.
    /// </summary>
    public string ArchitectureSummary { get; set; } = string.Empty;

    /// <summary>
    /// Execution status (in_progress, completed, failed).
    /// </summary>
    public string ExecutionStatus { get; set; } = "in_progress";
}

/// <summary>
/// Represents a completed task with its details.
/// </summary>
public class CompletedTaskDetail
{
    /// <summary>
    /// Task ID.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Task description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Dependencies of this task.
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Expected types to be generated.
    /// </summary>
    public List<string> ExpectedTypes { get; set; } = new();

    /// <summary>
    /// Namespace for the task.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Generated code for the task.
    /// </summary>
    public string GeneratedCode { get; set; } = string.Empty;

    /// <summary>
    /// Validation status.
    /// </summary>
    public string ValidationStatus { get; set; } = string.Empty;

    /// <summary>
    /// Number of validation attempts.
    /// </summary>
    public int ValidationAttempts { get; set; }

    /// <summary>
    /// UTC timestamp when the task was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Task summary.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Validation errors from the last validation attempt.
    /// Used for resume capability to provide context when regenerating code.
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Documentation status: "draft" or "final".
    /// </summary>
    public string DocumentationStatus { get; set; } = string.Empty;
}
