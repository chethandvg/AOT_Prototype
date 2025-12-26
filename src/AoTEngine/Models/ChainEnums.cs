namespace AoTEngine.Models;

/// <summary>
/// Policy for handling task failures in the dependency graph.
/// </summary>
public enum FailurePolicy
{
    /// <summary>
    /// Block execution of dependent tasks until failure is resolved.
    /// </summary>
    Block,

    /// <summary>
    /// Immediately fail the entire execution if any task fails.
    /// </summary>
    FailFast,

    /// <summary>
    /// Skip tasks that depend on failed tasks.
    /// </summary>
    SkipFailed,

    /// <summary>
    /// Skip tasks with missing dependencies.
    /// </summary>
    SkipMissing
}

/// <summary>
/// Type of aggregation for result composition.
/// </summary>
public enum AggregationType
{
    /// <summary>
    /// Atomic result (leaf node).
    /// </summary>
    Atomic,

    /// <summary>
    /// Composite result (aggregated from children).
    /// </summary>
    Composite
}

/// <summary>
/// Strategy for retrying failed tasks.
/// </summary>
public enum RetryType
{
    /// <summary>
    /// Simple retry with the same parameters.
    /// </summary>
    SimpleRetry,

    /// <summary>
    /// Regenerate with additional constraints.
    /// </summary>
    RegenerateWithConstraints,

    /// <summary>
    /// Redecompose from parent node.
    /// </summary>
    RedecomposeFromParent,

    /// <summary>
    /// Further decompose the failed task.
    /// </summary>
    FurtherDecomposition,

    /// <summary>
    /// Continue from reconstructed context.
    /// </summary>
    ContinueFromReconstruction,

    /// <summary>
    /// Abort execution.
    /// </summary>
    Abort
}

/// <summary>
/// Type of code element for contract management.
/// </summary>
public enum ElementType
{
    /// <summary>
    /// Interface definition.
    /// </summary>
    Interface,

    /// <summary>
    /// Enum definition.
    /// </summary>
    Enum,

    /// <summary>
    /// Class signature (abstract/sealed/base).
    /// </summary>
    ClassSignature,

    /// <summary>
    /// Unknown or unrecognized element type.
    /// </summary>
    Unknown
}

/// <summary>
/// Type of conflict detected in contract synchronization.
/// </summary>
public enum ContractConflictType
{
    /// <summary>
    /// Incompatible type definitions (different members/signatures).
    /// </summary>
    IncompatibleDefinition,

    /// <summary>
    /// Duplicate type definition with same signature.
    /// </summary>
    DuplicateDefinition,

    /// <summary>
    /// Signature mismatch (parameters, return type).
    /// </summary>
    SignatureMismatch
}

/// <summary>
/// Strategy for resolving contract conflicts.
/// </summary>
public enum ResolutionStrategy
{
    /// <summary>
    /// Keep the existing contract version.
    /// </summary>
    KeepExisting,

    /// <summary>
    /// Replace with the proposed contract version.
    /// </summary>
    KeepProposed,

    /// <summary>
    /// Merge both contracts (if possible).
    /// </summary>
    Merge,

    /// <summary>
    /// Requires manual review and resolution.
    /// </summary>
    RequiresManualReview
}
