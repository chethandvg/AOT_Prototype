namespace AoTEngine.Models;

/// <summary>
/// Represents the complete documentation for a generated project.
/// Contains all task summaries, architecture overview, and metadata.
/// </summary>
public class ProjectDocumentation
{
    /// <summary>
    /// The original user request that initiated the code generation.
    /// </summary>
    public string ProjectRequest { get; set; } = string.Empty;

    /// <summary>
    /// Any global assumptions or clarifications made during generation.
    /// </summary>
    public List<string> GlobalAssumptions { get; set; } = new();

    /// <summary>
    /// Collection of task summary records for all tasks in the project.
    /// </summary>
    public List<TaskSummaryRecord> TaskRecords { get; set; } = new();

    /// <summary>
    /// High-level architecture summary describing the overall structure.
    /// </summary>
    public string HighLevelArchitectureSummary { get; set; } = string.Empty;

    /// <summary>
    /// Index mapping type/class names to their originating task IDs.
    /// </summary>
    public Dictionary<string, string> ModuleIndex { get; set; } = new();

    /// <summary>
    /// Human-readable summary of the dependency graph.
    /// </summary>
    public string DependencyGraphSummary { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the documentation was generated.
    /// </summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Description of the decomposition from the AoT engine.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
