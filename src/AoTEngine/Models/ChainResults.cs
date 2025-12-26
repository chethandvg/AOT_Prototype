namespace AoTEngine.Models;

/// <summary>
/// Result of contract registration operation.
/// </summary>
public class ContractRegistrationResult
{
    /// <summary>
    /// Indicates whether registration was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Registered contract version.
    /// </summary>
    public VersionedContract? RegisteredContract { get; set; }

    /// <summary>
    /// Type of conflict detected (if any).
    /// </summary>
    public ContractConflictType? ConflictType { get; set; }

    /// <summary>
    /// Resolution strategy applied.
    /// </summary>
    public ResolutionStrategy? ResolutionStrategy { get; set; }

    /// <summary>
    /// Error message if registration failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Warnings during registration.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of branch synchronization operation.
/// </summary>
public class SynchronizationResult
{
    /// <summary>
    /// Indicates whether synchronization was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Synchronized contract versions.
    /// </summary>
    public Dictionary<string, int> SynchronizedVersions { get; set; } = new();

    /// <summary>
    /// Conflicts detected during synchronization.
    /// </summary>
    public List<ContractConflict> Conflicts { get; set; } = new();

    /// <summary>
    /// Error message if synchronization failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents a contract conflict during synchronization.
/// </summary>
public class ContractConflict
{
    /// <summary>
    /// Type name with conflict.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Type of conflict.
    /// </summary>
    public ContractConflictType ConflictType { get; set; }

    /// <summary>
    /// Existing contract version.
    /// </summary>
    public VersionedContract? ExistingContract { get; set; }

    /// <summary>
    /// Proposed contract version.
    /// </summary>
    public VersionedContract? ProposedContract { get; set; }

    /// <summary>
    /// Resolution strategy to apply.
    /// </summary>
    public ResolutionStrategy ResolutionStrategy { get; set; }
}

/// <summary>
/// Result of dependency registration.
/// </summary>
public class RegistrationResult
{
    /// <summary>
    /// Indicates whether registration was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if registration failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether a cycle was detected.
    /// </summary>
    public bool CycleDetected { get; set; }

    /// <summary>
    /// Cycle path if detected.
    /// </summary>
    public List<string> CyclePath { get; set; } = new();
}

/// <summary>
/// Result of checkpoint recovery operation.
/// </summary>
public class RecoveryResult
{
    /// <summary>
    /// Indicates whether recovery was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Recovered tree snapshot.
    /// </summary>
    public SerializableTreeSnapshot? RecoveredSnapshot { get; set; }

    /// <summary>
    /// Nodes that need reconstruction due to expired response IDs.
    /// </summary>
    public List<string> NodesNeedingReconstruction { get; set; } = new();

    /// <summary>
    /// Error message if recovery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Warnings during recovery.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of hierarchical aggregation.
/// </summary>
public class AggregatedResult
{
    /// <summary>
    /// Type of aggregation.
    /// </summary>
    public AggregationType Type { get; set; }

    /// <summary>
    /// Aggregated content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Summary of the aggregated content.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Child results (for composite aggregation).
    /// </summary>
    public List<AggregatedResult> Children { get; set; } = new();

    /// <summary>
    /// Token count of the aggregated content.
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// Whether detail was preserved or compressed.
    /// </summary>
    public bool DetailPreserved { get; set; }

    /// <summary>
    /// External storage reference for deep content.
    /// </summary>
    public string? ExternalStorageRef { get; set; }
}

/// <summary>
/// Context lineage for tracing context flow through the chain.
/// </summary>
public class ContextLineage
{
    /// <summary>
    /// Chain of response IDs.
    /// </summary>
    public List<string> ResponseChain { get; set; } = new();

    /// <summary>
    /// Depth in the decomposition tree.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Parent node ID.
    /// </summary>
    public string? ParentNodeId { get; set; }

    /// <summary>
    /// Ancestor node IDs.
    /// </summary>
    public List<string> AncestorNodeIds { get; set; } = new();
}

/// <summary>
/// Checkpoint data for recovery.
/// </summary>
public class Checkpoint
{
    /// <summary>
    /// Checkpoint ID.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Tree snapshot at checkpoint time.
    /// </summary>
    public SerializableTreeSnapshot Snapshot { get; set; } = new();

    /// <summary>
    /// Checkpoint timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Description of checkpoint state.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Execution plan for task orchestration.
/// </summary>
public class ExecutionPlan
{
    /// <summary>
    /// Execution waves (groups of parallel tasks).
    /// </summary>
    public List<ExecutionWave> Waves { get; set; } = new();

    /// <summary>
    /// Critical path tasks.
    /// </summary>
    public List<string> CriticalPath { get; set; } = new();

    /// <summary>
    /// Total estimated execution time.
    /// </summary>
    public TimeSpan EstimatedDuration { get; set; }
}

/// <summary>
/// Execution wave (group of tasks that can run in parallel).
/// </summary>
public class ExecutionWave
{
    /// <summary>
    /// Wave number.
    /// </summary>
    public int WaveNumber { get; set; }

    /// <summary>
    /// Task IDs in this wave.
    /// </summary>
    public List<string> TaskIds { get; set; } = new();

    /// <summary>
    /// Estimated duration for this wave.
    /// </summary>
    public TimeSpan EstimatedDuration { get; set; }

    /// <summary>
    /// Whether this wave is on the critical path.
    /// </summary>
    public bool IsCritical { get; set; }
}
