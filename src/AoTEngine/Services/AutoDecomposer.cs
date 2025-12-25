using AoTEngine.Models;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Service for automatically decomposing complex tasks into smaller subtasks.
/// Part 1: OpenAI integration for intelligent decomposition.
/// </summary>
public partial class AutoDecomposer
{
    private readonly OpenAIService _openAIService;
    private readonly TaskComplexityAnalyzer _complexityAnalyzer;
    private readonly ChatClient _chatClient;
    private const int MaxRetries = 3;
    private const int DefaultMaxLineThreshold = 100;

    public AutoDecomposer(OpenAIService openAIService, string apiKey, string model = "gpt-4")
    {
        _openAIService = openAIService;
        _complexityAnalyzer = new TaskComplexityAnalyzer();
        _chatClient = new ChatClient(model, apiKey);
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
    /// Gets subtasks from OpenAI based on decomposition type.
    /// </summary>
    private async Task<List<TaskNode>?> GetSubtasksFromOpenAIAsync(
        TaskNode task,
        ComplexityMetrics metrics,
        DecompositionType type,
        int maxLineThreshold)
    {
        var systemPrompt = GetDecompositionSystemPrompt(type, maxLineThreshold);
        var userPrompt = GetDecompositionUserPrompt(task, metrics, type, maxLineThreshold);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var completion = await _chatClient.CompleteChatAsync(messages);
                var contentParts = completion.Value.Content;

                if (contentParts == null || contentParts.Count == 0)
                {
                    if (attempt == MaxRetries - 1)
                    {
                        throw new InvalidOperationException("OpenAI returned no content for decomposition.");
                    }
                    await Task.Delay(1000 * (attempt + 1));
                    continue;
                }

                var content = contentParts[0].Text.Trim();
                var response = JsonConvert.DeserializeObject<SubtaskDecompositionResponse>(content);

                if (response?.Subtasks != null && response.Subtasks.Any())
                {
                    return response.Subtasks;
                }
            }
            catch (HttpRequestException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"HTTP error during decomposition (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (JsonException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"JSON parsing error during decomposition (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        return null;
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

        // Interface + implementation pattern detected
        if (task.Description.ToLowerInvariant().Contains("interface") &&
            task.Description.ToLowerInvariant().Contains("implementation"))
        {
            return DecompositionType.InterfaceBased;
        }

        // Complex service with multiple concerns -> Layer-based
        if (task.Description.ToLowerInvariant().Contains("service") &&
            (task.Description.ToLowerInvariant().Contains("validation") ||
             task.Description.ToLowerInvariant().Contains("repository")))
        {
            return DecompositionType.LayerBased;
        }

        // Default to functional decomposition
        return DecompositionType.Functional;
    }

    /// <summary>
    /// Gets the system prompt for decomposition based on type.
    /// </summary>
    private string GetDecompositionSystemPrompt(DecompositionType type, int maxLineThreshold)
    {
        var basePrompt = $@"You are an expert C# software architect specializing in code decomposition.
Your task is to break down complex coding tasks into smaller subtasks.

CRITICAL CONSTRAINTS:
1. Each subtask must generate NO MORE than {maxLineThreshold} lines of code
2. Subtasks must be independent and compilable
3. Maintain proper dependencies between subtasks
4. Ensure no circular dependencies

";
        return type switch
        {
            DecompositionType.PartialClass => basePrompt + GetPartialClassPrompt(),
            DecompositionType.InterfaceBased => basePrompt + GetInterfaceBasedPrompt(),
            DecompositionType.LayerBased => basePrompt + GetLayerBasedPrompt(),
            _ => basePrompt + GetFunctionalPrompt()
        };
    }

    private string GetFunctionalPrompt() => @"
FUNCTIONAL DECOMPOSITION STRATEGY:
- Split by logical functionality (e.g., models, services, utilities)
- Each subtask handles one cohesive concern
- Use clear naming: task1_models, task1_service, task1_utils";

    private string GetPartialClassPrompt() => @"
PARTIAL CLASS DECOMPOSITION STRATEGY:
- Use C# partial classes to split large classes
- First part contains: fields, properties, constructors
- Subsequent parts contain: method groups by functionality
- Use naming pattern: ClassName.Part1.cs, ClassName.Part2.cs
- All parts must be in the SAME namespace";

    private string GetInterfaceBasedPrompt() => @"
INTERFACE-BASED DECOMPOSITION STRATEGY:
- First subtask: Define interfaces and contracts
- Second subtask: Implement the interfaces
- Use dependency injection patterns
- Keep interfaces in a separate namespace if needed";

    private string GetLayerBasedPrompt() => @"
LAYER-BASED DECOMPOSITION STRATEGY:
- Split by architectural layers (Models, Services, Repository, etc.)
- Maintain proper layer dependencies (top to bottom)
- Use abstractions between layers";
}

/// <summary>
/// Response model for subtask decomposition from OpenAI.
/// </summary>
internal class SubtaskDecompositionResponse
{
    [JsonProperty("subtasks")]
    public List<TaskNode> Subtasks { get; set; } = new();

    [JsonProperty("shared_fields")]
    public List<string>? SharedFields { get; set; }

    [JsonProperty("shared_interfaces")]
    public List<string>? SharedInterfaces { get; set; }
}
