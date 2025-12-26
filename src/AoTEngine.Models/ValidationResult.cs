namespace AoTEngine.Models;

/// <summary>
/// Result of code validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Indicates whether validation was successful.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
