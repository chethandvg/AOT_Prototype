namespace AoTEngine.Models;

/// <summary>
/// Represents a strategy for decomposing complex tasks into smaller subtasks.
/// </summary>
public class TaskDecompositionStrategy
{
    /// <summary>
    /// Original task that needs decomposition.
    /// </summary>
    public string OriginalTaskId { get; set; } = string.Empty;

    /// <summary>
    /// Type of decomposition strategy to use.
    /// </summary>
    public DecompositionType Type { get; set; }

    /// <summary>
    /// List of generated subtasks from decomposition.
    /// </summary>
    public List<TaskNode> Subtasks { get; set; } = new();

    /// <summary>
    /// Partial class configuration if using partial class strategy.
    /// </summary>
    public PartialClassConfig? PartialClassConfig { get; set; }

    /// <summary>
    /// Shared state that needs to be coordinated across subtasks.
    /// </summary>
    public SharedStateInfo? SharedState { get; set; }

    /// <summary>
    /// Indicates whether the decomposition was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Error message if decomposition failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Estimated total line count across all subtasks.
    /// </summary>
    public int EstimatedTotalLines { get; set; }
}

/// <summary>
/// Types of decomposition strategies available.
/// </summary>
public enum DecompositionType
{
    /// <summary>
    /// Functional decomposition - split by logical functions.
    /// </summary>
    Functional,

    /// <summary>
    /// Partial class decomposition - split into partial classes.
    /// </summary>
    PartialClass,

    /// <summary>
    /// Interface-based decomposition - separate interface from implementation.
    /// </summary>
    InterfaceBased,

    /// <summary>
    /// Layer-based decomposition - split by architectural layers.
    /// </summary>
    LayerBased
}

/// <summary>
/// Configuration for partial class decomposition strategy.
/// </summary>
public class PartialClassConfig
{
    /// <summary>
    /// Base class name for partial classes.
    /// </summary>
    public string BaseClassName { get; set; } = string.Empty;

    /// <summary>
    /// Namespace for partial classes.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Number of partial class parts to generate.
    /// </summary>
    public int PartCount { get; set; }

    /// <summary>
    /// Naming convention for partial class parts (e.g., "ClassName.Part1.cs").
    /// </summary>
    public string NamingPattern { get; set; } = "{ClassName}.Part{PartNumber}";

    /// <summary>
    /// Methods to be distributed across partial class parts.
    /// </summary>
    public List<MethodDistribution> MethodDistributions { get; set; } = new();
}

/// <summary>
/// Represents how methods are distributed across partial class parts.
/// </summary>
public class MethodDistribution
{
    /// <summary>
    /// Part number (1-based).
    /// </summary>
    public int PartNumber { get; set; }

    /// <summary>
    /// Method names assigned to this part.
    /// </summary>
    public List<string> MethodNames { get; set; } = new();

    /// <summary>
    /// Description of what this part handles.
    /// </summary>
    public string PartDescription { get; set; } = string.Empty;
}

/// <summary>
/// Information about shared state across subtasks.
/// </summary>
public class SharedStateInfo
{
    /// <summary>
    /// Fields that are shared across partial class parts.
    /// </summary>
    public List<SharedField> SharedFields { get; set; } = new();

    /// <summary>
    /// Interfaces that need to be implemented across parts.
    /// </summary>
    public List<string> SharedInterfaces { get; set; } = new();

    /// <summary>
    /// Constructor parameters that need coordination.
    /// </summary>
    public List<string> ConstructorParameters { get; set; } = new();
}

/// <summary>
/// Represents a shared field in partial classes.
/// </summary>
public class SharedField
{
    /// <summary>
    /// Field name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Part number where the field is defined.
    /// </summary>
    public int DefinedInPart { get; set; }

    /// <summary>
    /// Whether the field is read-only.
    /// </summary>
    public bool IsReadOnly { get; set; }
}
