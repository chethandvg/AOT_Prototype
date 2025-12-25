using AoTEngine.Models;
using System.Text.RegularExpressions;

namespace AoTEngine.Services;

/// <summary>
/// Service for analyzing task complexity to determine if decomposition is needed.
/// Part 1: Core analysis functionality.
/// </summary>
public partial class TaskComplexityAnalyzer
{
    private const int DefaultMaxLineThreshold = 300;
    private const int HighComplexityThreshold = 70;
    private const int MediumComplexityThreshold = 40;

    // Complexity factor weights
    private const double TypeWeight = 0.25;
    private const double DependencyWeight = 0.20;
    private const double MethodWeight = 0.25;
    private const double DescriptionWeight = 0.30;

    // Keywords that indicate complexity in task descriptions (lowercase for direct comparison)
    private static readonly string[] ComplexityKeywords = new[]
    {
        "complex", "comprehensive", "full", "complete", "advanced",
        "implement", "integration", "multiple", "various", "all",
        "crud", "api", "service", "repository", "controller",
        "authentication", "authorization", "validation", "caching"
    };

    // Compiled regex patterns for multi-method implementations (performance optimization)
    private static readonly Regex[] MethodIndicatorRegexes = new[]
    {
        new Regex(@"create\s+\w+\s+methods?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"implement\s+\w+\s+operations?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"add\s+\w+\s+functionality", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"CRUD\s+operations?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"get\s+set\s+update\s+delete", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"async\s+methods?", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Compiled regex for type pattern matching (performance optimization)
    private static readonly Regex TypePatternRegex = new Regex(
        @"(class|interface|enum|record).*and.*(class|interface|enum|record)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Analyzes a task to determine its complexity metrics.
    /// </summary>
    /// <param name="task">The task to analyze.</param>
    /// <param name="maxLineThreshold">Maximum allowed lines of code (default: 100).</param>
    /// <returns>Complexity metrics for the task.</returns>
    public ComplexityMetrics AnalyzeTask(TaskNode task, int maxLineThreshold = DefaultMaxLineThreshold)
    {
        var metrics = new ComplexityMetrics
        {
            MaxLineThreshold = maxLineThreshold
        };

        // Calculate individual complexity factors
        var breakdown = new ComplexityBreakdown();

        // Type complexity (based on expected types)
        breakdown.TypeComplexity = CalculateTypeComplexity(task, breakdown.ContributingFactors);
        metrics.ExpectedTypeCount = task.ExpectedTypes?.Count ?? 0;

        // Dependency complexity
        breakdown.DependencyComplexity = CalculateDependencyComplexity(task, breakdown.ContributingFactors);
        metrics.DependencyCount = task.Dependencies?.Count ?? 0;

        // Method complexity (estimated from description)
        breakdown.MethodComplexity = CalculateMethodComplexity(task, breakdown.ContributingFactors);
        metrics.EstimatedMethodCount = EstimateMethodCount(task);

        // Description complexity
        breakdown.DescriptionComplexity = CalculateDescriptionComplexity(task, breakdown.ContributingFactors);

        // Calculate overall complexity score
        metrics.ComplexityScore = CalculateOverallScore(breakdown);
        metrics.Breakdown = breakdown;

        // Estimate line count
        metrics.EstimatedLineCount = EstimateLineCount(task, metrics);
        metrics.EstimationConfidence = CalculateEstimationConfidence(task, metrics);

        // Determine if decomposition is needed
        metrics.RequiresDecomposition = DetermineDecompositionNeed(metrics);
        if (metrics.RequiresDecomposition)
        {
            metrics.RecommendedSubtaskCount = CalculateRecommendedSubtasks(metrics);
        }

        return metrics;
    }

    /// <summary>
    /// Calculates type complexity score (0-25).
    /// </summary>
    private int CalculateTypeComplexity(TaskNode task, List<string> factors)
    {
        var typeCount = task.ExpectedTypes?.Count ?? 0;
        
        if (typeCount == 0)
        {
            // Estimate from description
            typeCount = EstimateTypeCountFromDescription(task.Description);
        }

        var score = Math.Min(25, typeCount * 5);
        
        if (typeCount > 3)
        {
            factors.Add($"Multiple types expected ({typeCount} types)");
        }

        return score;
    }

    /// <summary>
    /// Calculates dependency complexity score (0-25).
    /// </summary>
    private int CalculateDependencyComplexity(TaskNode task, List<string> factors)
    {
        var depCount = task.Dependencies?.Count ?? 0;
        var consumedTypeCount = task.ConsumedTypes?.Values.Sum(v => v.Count) ?? 0;

        var score = Math.Min(25, (depCount * 3) + (consumedTypeCount * 2));
        
        if (depCount > 2)
        {
            factors.Add($"Multiple dependencies ({depCount} dependencies)");
        }
        if (consumedTypeCount > 5)
        {
            factors.Add($"Many consumed types ({consumedTypeCount} types from dependencies)");
        }

        return score;
    }

    /// <summary>
    /// Estimates type count from task description.
    /// </summary>
    private int EstimateTypeCountFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return 1;

        var lowerDesc = description.ToLowerInvariant();
        var count = 1;

        // Check for patterns indicating multiple types using compiled regex
        if (TypePatternRegex.IsMatch(lowerDesc))
            count += 2;
        if (lowerDesc.Contains("model") && lowerDesc.Contains("service"))
            count += 1;
        if (lowerDesc.Contains("dto") || lowerDesc.Contains("request") || lowerDesc.Contains("response"))
            count += 1;
        if (lowerDesc.Contains("interface") && lowerDesc.Contains("implementation"))
            count += 1;

        return count;
    }
}
