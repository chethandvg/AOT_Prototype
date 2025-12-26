namespace AoTEngine.Models;

/// <summary>
/// Represents complexity metrics for code generation tasks.
/// Used to determine if a task needs decomposition into smaller subtasks.
/// </summary>
public class ComplexityMetrics
{
    /// <summary>
    /// Estimated number of code lines that will be generated.
    /// </summary>
    public int EstimatedLineCount { get; set; }

    /// <summary>
    /// Number of expected types (classes, interfaces, enums) to be generated.
    /// </summary>
    public int ExpectedTypeCount { get; set; }

    /// <summary>
    /// Number of dependencies this task has on other tasks.
    /// </summary>
    public int DependencyCount { get; set; }

    /// <summary>
    /// Number of methods expected to be generated.
    /// </summary>
    public int EstimatedMethodCount { get; set; }

    /// <summary>
    /// Complexity score calculated from various factors (0-100 scale).
    /// Higher scores indicate more complex tasks.
    /// </summary>
    public int ComplexityScore { get; set; }

    /// <summary>
    /// Indicates whether the task requires decomposition based on complexity.
    /// </summary>
    public bool RequiresDecomposition { get; set; }

    /// <summary>
    /// Recommended number of subtasks if decomposition is needed.
    /// </summary>
    public int RecommendedSubtaskCount { get; set; }

    /// <summary>
    /// Maximum line count threshold for code generation (default: 300).
    /// Tasks exceeding this threshold will be automatically decomposed into smaller subtasks.
    /// </summary>
    public int MaxLineThreshold { get; set; } = 300;

    /// <summary>
    /// Confidence level of the complexity estimation (0.0 to 1.0).
    /// Lower values indicate more uncertainty in the estimate.
    /// </summary>
    public double EstimationConfidence { get; set; }

    /// <summary>
    /// Detailed breakdown of complexity factors.
    /// </summary>
    public ComplexityBreakdown? Breakdown { get; set; }
}

/// <summary>
/// Detailed breakdown of complexity factors for analysis.
/// </summary>
public class ComplexityBreakdown
{
    /// <summary>
    /// Score based on expected type count (0-25).
    /// </summary>
    public int TypeComplexity { get; set; }

    /// <summary>
    /// Score based on dependency complexity (0-25).
    /// </summary>
    public int DependencyComplexity { get; set; }

    /// <summary>
    /// Score based on method count estimate (0-25).
    /// </summary>
    public int MethodComplexity { get; set; }

    /// <summary>
    /// Score based on task description analysis (0-25).
    /// </summary>
    public int DescriptionComplexity { get; set; }

    /// <summary>
    /// Factors that contributed to complexity score.
    /// </summary>
    public List<string> ContributingFactors { get; set; } = new();
}
