namespace AoTEngine.Models;

/// <summary>
/// Represents a node in the response chain tree for hierarchical decomposition.
/// </summary>
public class ResponseChainNode
{
    /// <summary>
    /// Unique identifier for this node.
    /// </summary>
    public string NodeId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Associated task ID from the task graph.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI response ID for this node's generation.
    /// </summary>
    public string? ResponseId { get; set; }

    /// <summary>
    /// Parent response ID that this node chains from.
    /// </summary>
    public string? ParentResponseId { get; set; }

    /// <summary>
    /// Depth in the decomposition tree (0 = root).
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Child nodes in the decomposition tree.
    /// </summary>
    public List<ResponseChainNode> Children { get; set; } = new();

    /// <summary>
    /// Dependencies across different branches of the tree.
    /// </summary>
    public List<string> CrossBranchDependencies { get; set; } = new();

    /// <summary>
    /// Indicates whether this task is atomic (cannot be further decomposed).
    /// </summary>
    public bool IsAtomic { get; set; }

    /// <summary>
    /// Result of executing this atomic task.
    /// </summary>
    public AtomicExecutionResult? ExecutionResult { get; set; }

    /// <summary>
    /// Timestamp when this node was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this node was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Complexity metrics for this node.
    /// </summary>
    public ComplexityMetrics? Metrics { get; set; }
}

/// <summary>
/// Result of executing an atomic task.
/// </summary>
public class AtomicExecutionResult
{
    /// <summary>
    /// Generated code from the atomic task.
    /// </summary>
    public string GeneratedCode { get; set; } = string.Empty;

    /// <summary>
    /// Validation status of the generated code.
    /// </summary>
    public bool IsValidated { get; set; }

    /// <summary>
    /// Validation errors if any.
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Type contract extracted from the generated code.
    /// </summary>
    public string? TypeContract { get; set; }

    /// <summary>
    /// Summary of the generated code.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Timestamp when execution completed.
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Compressed context for managing context size.
/// </summary>
public class CompressedContext
{
    /// <summary>
    /// Compressed context text.
    /// </summary>
    public string CompressedText { get; set; } = string.Empty;

    /// <summary>
    /// Original context text (stored separately).
    /// </summary>
    public string? OriginalText { get; set; }

    /// <summary>
    /// Compression ratio achieved.
    /// </summary>
    public double CompressionRatio { get; set; }

    /// <summary>
    /// Token count before compression.
    /// </summary>
    public int OriginalTokenCount { get; set; }

    /// <summary>
    /// Token count after compression.
    /// </summary>
    public int CompressedTokenCount { get; set; }

    /// <summary>
    /// Indicates whether compression was lossy.
    /// </summary>
    public bool IsLossy { get; set; }

    /// <summary>
    /// Verification status of compressed context.
    /// </summary>
    public bool VerificationPassed { get; set; }

    /// <summary>
    /// Cold storage path for original context.
    /// </summary>
    public string? ColdStoragePath { get; set; }
}

/// <summary>
/// Versioned contract snapshot for point-in-time consistency.
/// </summary>
public class VersionedContract
{
    /// <summary>
    /// Contract version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Contract content (interface, enum, model signatures).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Type name of the contract.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Namespace of the contract.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this version was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Hash of the contract content for change detection.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;
}

/// <summary>
/// Serializable snapshot of the decomposition tree for checkpointing.
/// </summary>
public class SerializableTreeSnapshot
{
    /// <summary>
    /// Root node of the tree.
    /// </summary>
    public ResponseChainNode? Root { get; set; }

    /// <summary>
    /// Flattened list of all nodes for easier access.
    /// </summary>
    public List<ResponseChainNode> AllNodes { get; set; } = new();

    /// <summary>
    /// Contract versions at the time of snapshot.
    /// </summary>
    public Dictionary<string, VersionedContract> ContractSnapshot { get; set; } = new();

    /// <summary>
    /// Timestamp when snapshot was created.
    /// </summary>
    public DateTime SnapshotTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Maximum depth reached in the tree.
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// Total number of atomic nodes in the tree.
    /// </summary>
    public int AtomicNodeCount { get; set; }
}
