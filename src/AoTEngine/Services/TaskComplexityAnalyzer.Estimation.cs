using AoTEngine.Models;
using System.Text.RegularExpressions;

namespace AoTEngine.Services;

/// <summary>
/// Service for analyzing task complexity to determine if decomposition is needed.
/// Part 2: Estimation logic and scoring algorithms.
/// </summary>
public partial class TaskComplexityAnalyzer
{
    /// <summary>
    /// Calculates method complexity score (0-25).
    /// </summary>
    private int CalculateMethodComplexity(TaskNode task, List<string> factors)
    {
        var methodCount = EstimateMethodCount(task);
        var score = Math.Min(25, methodCount * 3);

        if (methodCount > 5)
        {
            factors.Add($"Many methods expected ({methodCount} methods)");
        }

        // Check for async patterns which add complexity
        if (task.Description.ToLowerInvariant().Contains("async"))
        {
            score = Math.Min(25, score + 3);
            factors.Add("Asynchronous operations required");
        }

        return score;
    }

    /// <summary>
    /// Calculates description complexity score (0-25).
    /// </summary>
    private int CalculateDescriptionComplexity(TaskNode task, List<string> factors)
    {
        if (string.IsNullOrWhiteSpace(task.Description))
            return 0;

        var lowerDesc = task.Description.ToLowerInvariant();
        var score = 0;

        // Check for complexity keywords
        var matchedKeywords = ComplexityKeywords
            .Where(k => lowerDesc.Contains(k.ToLowerInvariant()))
            .ToList();

        score += Math.Min(15, matchedKeywords.Count * 3);
        
        if (matchedKeywords.Count > 3)
        {
            factors.Add($"Complex requirements ({string.Join(", ", matchedKeywords.Take(3))}...)");
        }

        // Check description length (longer descriptions often mean more complexity)
        var wordCount = task.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > 30)
        {
            score += 5;
            factors.Add("Detailed requirements specification");
        }
        else if (wordCount > 50)
        {
            score += 10;
            factors.Add("Very detailed requirements specification");
        }

        return Math.Min(25, score);
    }

    /// <summary>
    /// Estimates the number of methods from task description.
    /// </summary>
    private int EstimateMethodCount(TaskNode task)
    {
        if (string.IsNullOrWhiteSpace(task.Description))
            return 2; // Default assumption

        var lowerDesc = task.Description.ToLowerInvariant();
        var count = 1; // At least one method

        // Check for method indicator patterns
        foreach (var pattern in MethodIndicatorPatterns)
        {
            if (Regex.IsMatch(lowerDesc, pattern, RegexOptions.IgnoreCase))
            {
                count += 2;
            }
        }

        // CRUD operations typically mean 4+ methods
        if (lowerDesc.Contains("crud"))
        {
            count = Math.Max(count, 4);
        }

        // Service implementations typically have multiple methods
        if (lowerDesc.Contains("service") && lowerDesc.Contains("implement"))
        {
            count = Math.Max(count, 3);
        }

        // Repository patterns typically have standard methods
        if (lowerDesc.Contains("repository"))
        {
            count = Math.Max(count, 5);
        }

        return count;
    }

    /// <summary>
    /// Calculates the overall complexity score from breakdown.
    /// </summary>
    private int CalculateOverallScore(ComplexityBreakdown breakdown)
    {
        var weightedScore = 
            (breakdown.TypeComplexity * TypeWeight) +
            (breakdown.DependencyComplexity * DependencyWeight) +
            (breakdown.MethodComplexity * MethodWeight) +
            (breakdown.DescriptionComplexity * DescriptionWeight);

        return (int)Math.Round(weightedScore * 4); // Scale to 0-100
    }

    /// <summary>
    /// Estimates the number of lines of code that will be generated.
    /// </summary>
    private int EstimateLineCount(TaskNode task, ComplexityMetrics metrics)
    {
        // Base lines per type (class declaration, properties, etc.)
        var linesPerType = 25;
        
        // Lines per method (average)
        var linesPerMethod = 12;

        // Base calculation
        var estimatedLines = 
            (metrics.ExpectedTypeCount * linesPerType) +
            (metrics.EstimatedMethodCount * linesPerMethod);

        // Add overhead for using statements, namespace, etc.
        estimatedLines += 10;

        // Adjust based on complexity score
        if (metrics.ComplexityScore > HighComplexityThreshold)
        {
            estimatedLines = (int)(estimatedLines * 1.5);
        }
        else if (metrics.ComplexityScore > MediumComplexityThreshold)
        {
            estimatedLines = (int)(estimatedLines * 1.25);
        }

        // Minimum lines estimate
        return Math.Max(20, estimatedLines);
    }

    /// <summary>
    /// Calculates confidence in the line count estimation.
    /// </summary>
    private double CalculateEstimationConfidence(TaskNode task, ComplexityMetrics metrics)
    {
        var confidence = 0.5; // Base confidence

        // Higher confidence if expected types are explicitly defined
        if (task.ExpectedTypes?.Any() == true)
        {
            confidence += 0.2;
        }

        // Higher confidence if description is detailed
        var wordCount = task.Description?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        if (wordCount > 20)
        {
            confidence += 0.1;
        }

        // Lower confidence for very high complexity (more uncertainty)
        if (metrics.ComplexityScore > HighComplexityThreshold)
        {
            confidence -= 0.1;
        }

        // Ensure confidence stays in valid range
        return Math.Clamp(confidence, 0.1, 0.9);
    }

    /// <summary>
    /// Determines if decomposition is needed based on metrics.
    /// </summary>
    private bool DetermineDecompositionNeed(ComplexityMetrics metrics)
    {
        // Primary check: estimated line count exceeds threshold
        if (metrics.EstimatedLineCount > metrics.MaxLineThreshold)
        {
            return true;
        }

        // Secondary check: very high complexity score
        if (metrics.ComplexityScore >= 80)
        {
            return true;
        }

        // Check type count (many types in one task is problematic)
        if (metrics.ExpectedTypeCount > 3)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates the recommended number of subtasks for decomposition.
    /// </summary>
    private int CalculateRecommendedSubtasks(ComplexityMetrics metrics)
    {
        // Calculate based on line count
        var lineBasedCount = (int)Math.Ceiling(
            (double)metrics.EstimatedLineCount / metrics.MaxLineThreshold);

        // Calculate based on type count
        var typeBasedCount = Math.Max(1, metrics.ExpectedTypeCount);

        // Use the higher of the two
        var recommended = Math.Max(lineBasedCount, typeBasedCount);

        // Limit to reasonable range
        return Math.Clamp(recommended, 2, 10);
    }

    /// <summary>
    /// Analyzes multiple tasks and returns those requiring decomposition.
    /// </summary>
    public List<(TaskNode Task, ComplexityMetrics Metrics)> AnalyzeTasksForDecomposition(
        List<TaskNode> tasks,
        int maxLineThreshold = DefaultMaxLineThreshold)
    {
        var results = new List<(TaskNode, ComplexityMetrics)>();

        foreach (var task in tasks)
        {
            var metrics = AnalyzeTask(task, maxLineThreshold);
            if (metrics.RequiresDecomposition)
            {
                results.Add((task, metrics));
            }
        }

        return results;
    }
}
