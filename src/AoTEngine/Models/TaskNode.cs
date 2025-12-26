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

    /// <summary>
    /// Expected namespace for this task's code.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Expected type names to be generated in this task.
    /// </summary>
    public List<string> ExpectedTypes { get; set; } = new();

    /// <summary>
    /// Type signatures extracted from generated code (interfaces, public APIs).
    /// </summary>
    public string TypeContract { get; set; } = string.Empty;

    /// <summary>
    /// Required NuGet packages or assemblies for this task.
    /// </summary>
    public List<string> RequiredPackages { get; set; } = new();

    /// <summary>
    /// Types consumed from dependent tasks. Key: task ID, Value: list of type names.
    /// </summary>
    public Dictionary<string, List<string>> ConsumedTypes { get; set; } = new();

    /// <summary>
    /// Summary explanation of the task's generated code.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Documentation status: "draft" (generated before validation) or "final" (validated successfully).
    /// </summary>
    public string DocumentationStatus { get; set; } = string.Empty;

    /// <summary>
    /// The model used to generate the summary (e.g., "gpt-4o-mini").
    /// </summary>
    public string SummaryModel { get; set; } = string.Empty;

    /// <summary>
    /// Number of validation attempts made before the task passed.
    /// </summary>
    public int ValidationAttemptCount { get; set; }

    /// <summary>
    /// UTC timestamp when the summary was generated.
    /// </summary>
    public DateTime? SummaryGeneratedAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the task was completed.
    /// </summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Associated response chain node for hierarchical decomposition.
    /// </summary>
    public ResponseChainNode? ChainNode { get; set; }
}
