using AoTEngine.Models;

namespace AoTEngine.Services;

/// <summary>
/// Service for automatically decomposing complex tasks into smaller subtasks.
/// Part 1: OpenAI integration for intelligent decomposition.
/// </summary>
public partial class AutoDecomposer
{
    private readonly OpenAIService _openAIService;
    private readonly TaskComplexityAnalyzer _complexityAnalyzer;
    private const int DefaultMaxLineThreshold = 300;

    public AutoDecomposer(OpenAIService openAIService)
    {
        _openAIService = openAIService;
        _complexityAnalyzer = new TaskComplexityAnalyzer();
    }

    /// <summary>
    /// Decomposes a complex task into smaller subtasks using OpenAI.
    /// </summary>
    public async Task<TaskDecompositionStrategy> DecomposeComplexTaskAsync(
        TaskNode task,
        ComplexityMetrics metrics,
        int maxLineThreshold = DefaultMaxLineThreshold)
    {
        var strategy = new TaskDecompositionStrategy
        {
            OriginalTaskId = task.Id
        };

        try
        {
            // Determine best decomposition type
            strategy.Type = DetermineDecompositionType(task, metrics);

            Console.WriteLine($"📋 Decomposing task '{task.Id}' using {strategy.Type} strategy...");

            // Get decomposition from OpenAI
            var subtasks = await GetSubtasksFromOpenAIAsync(task, metrics, strategy.Type, maxLineThreshold);

            if (subtasks == null || !subtasks.Any())
            {
                strategy.IsSuccessful = false;
                strategy.ErrorMessage = "OpenAI did not return valid subtasks";
                return strategy;
            }

            // Validate and process subtasks
            strategy.Subtasks = ProcessAndValidateSubtasks(subtasks, task, metrics);

            // Set up partial class configuration if needed
            if (strategy.Type == DecompositionType.PartialClass)
            {
                strategy.PartialClassConfig = CreatePartialClassConfig(task, strategy.Subtasks);
                strategy.SharedState = IdentifySharedState(task, strategy.Subtasks);
            }

            strategy.EstimatedTotalLines = strategy.Subtasks.Sum(s => 
                _complexityAnalyzer.AnalyzeTask(s, maxLineThreshold).EstimatedLineCount);
            strategy.IsSuccessful = true;

            Console.WriteLine($"✅ Created {strategy.Subtasks.Count} subtasks for '{task.Id}'");
        }
        catch (Exception ex)
        {
            strategy.IsSuccessful = false;
            strategy.ErrorMessage = ex.Message;
            Console.WriteLine($"❌ Failed to decompose task '{task.Id}': {ex.Message}");
        }

        return strategy;
    }

    /// <summary>
    /// Gets subtasks from OpenAI using the OpenAIService.
    /// </summary>
    private async Task<List<TaskNode>?> GetSubtasksFromOpenAIAsync(
        TaskNode task,
        ComplexityMetrics metrics,
        DecompositionType type,
        int maxLineThreshold)
    {
        try
        {
            // Use OpenAIService's decomposition method
            var subtasks = await _openAIService.DecomposeComplexTaskAsync(
                task,
                metrics.RecommendedSubtaskCount,
                maxLineThreshold);
            
            return subtasks;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting subtasks from OpenAI: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Determines the best decomposition type based on task characteristics.
    /// </summary>
    private DecompositionType DetermineDecompositionType(TaskNode task, ComplexityMetrics metrics)
    {
        // Multiple types with no shared functionality -> Functional decomposition
        if (metrics.ExpectedTypeCount > 3)
        {
            return DecompositionType.Functional;
        }

        // Single large class -> Partial class decomposition
        if (metrics.ExpectedTypeCount <= 2 && metrics.EstimatedMethodCount > 5)
        {
            return DecompositionType.PartialClass;
        }

        // Check description for patterns - handle null safely
        var description = task.Description;
        if (string.IsNullOrEmpty(description))
        {
            // No description available; default to functional decomposition
            return DecompositionType.Functional;
        }

        var descriptionLower = description.ToLowerInvariant();

        // Interface + implementation pattern detected
        if (descriptionLower.Contains("interface") &&
            descriptionLower.Contains("implementation"))
        {
            return DecompositionType.InterfaceBased;
        }

        // Complex service with multiple concerns -> Layer-based
        if (descriptionLower.Contains("service") &&
            (descriptionLower.Contains("validation") ||
             descriptionLower.Contains("repository")))
        {
            return DecompositionType.LayerBased;
        }

        // Default to functional decomposition
        return DecompositionType.Functional;
    }
}
