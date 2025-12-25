using AoTEngine.Models;

namespace AoTEngine.Core;

/// <summary>
/// Result of an AoT Engine execution.
/// </summary>
public class AoTResult
{
    /// <summary>
    /// The original user request.
    /// </summary>
    public string OriginalRequest { get; set; } = string.Empty;

    /// <summary>
    /// Description of the decomposition.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of executed tasks.
    /// </summary>
    public List<TaskNode> Tasks { get; set; } = new();

    /// <summary>
    /// Final merged code.
    /// </summary>
    public string FinalCode { get; set; } = string.Empty;

    /// <summary>
    /// Execution report.
    /// </summary>
    public string ExecutionReport { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Aggregated human-readable documentation for the project.
    /// </summary>
    public string FinalDocumentation { get; set; } = string.Empty;

    /// <summary>
    /// Complete project documentation with structured task summaries.
    /// </summary>
    public ProjectDocumentation? ProjectDocumentation { get; set; }

    /// <summary>
    /// Paths where documentation files were saved (if applicable).
    /// </summary>
    public DocumentationPaths? DocumentationPaths { get; set; }
}

/// <summary>
/// Contains paths to generated documentation files.
/// </summary>
public class DocumentationPaths
{
    /// <summary>
    /// Path to the markdown documentation file.
    /// </summary>
    public string? MarkdownPath { get; set; }

    /// <summary>
    /// Path to the JSON documentation file.
    /// </summary>
    public string? JsonPath { get; set; }

    /// <summary>
    /// Path to the JSONL training dataset file.
    /// </summary>
    public string? JsonlDatasetPath { get; set; }
}
