namespace AoTEngine.Models;

/// <summary>
/// Represents a single atomic task in the DAG (Directed Acyclic Graph).
/// </summary>
public class TaskNode
{
    /// <summary>
    /// Unique identifier for the task.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Description of the task to be performed.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of task IDs that this task depends on.
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Context information required for this task.
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Generated code snippet for this task.
    /// </summary>
    public string GeneratedCode { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the task has been completed.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Indicates whether the task passed validation.
    /// </summary>
    public bool IsValidated { get; set; }

    /// <summary>
    /// Validation error messages if any.
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Number of retry attempts made for this task.
    /// </summary>
    public int RetryCount { get; set; }
}
