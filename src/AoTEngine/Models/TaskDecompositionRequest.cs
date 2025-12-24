namespace AoTEngine.Models;

/// <summary>
/// Request model for task decomposition via OpenAI.
/// </summary>
public class TaskDecompositionRequest
{
    /// <summary>
    /// The user's original request to be decomposed.
    /// </summary>
    public string OriginalRequest { get; set; } = string.Empty;

    /// <summary>
    /// Additional context for the decomposition.
    /// </summary>
    public string Context { get; set; } = string.Empty;
}
