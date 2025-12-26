using AoTEngine.Models;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing task decomposition methods.
/// </summary>
public partial class OpenAIService
{
    // Safety margin for line count estimation (accounts for overhead like using statements, namespace declarations)
    private const int LineCountSafetyMargin = 10;

    /// <summary>
    /// Decomposes a complex task into smaller subtasks using OpenAI.
    /// Used when a task is estimated to generate more than the allowed line count.
    /// </summary>
    /// <param name="task">The task to decompose.</param>
    /// <param name="targetSubtaskCount">Number of subtasks to create.</param>
    /// <param name="maxLinesPerSubtask">Maximum lines per subtask (default: 300).</param>
    /// <returns>List of decomposed subtasks.</returns>
    public async Task<List<TaskNode>> DecomposeComplexTaskAsync(
        TaskNode task,
        int targetSubtaskCount,
        int maxLinesPerSubtask = 300)
    {
        // Subtract safety margin to account for using statements, namespace declarations, and other overhead
        var effectiveMaxLines = maxLinesPerSubtask - LineCountSafetyMargin;
        
        var systemPrompt = $@"You are an expert C# software architect. Decompose the given task into {targetSubtaskCount} smaller subtasks.

CRITICAL REQUIREMENTS:
1. Each subtask must generate NO MORE than {effectiveMaxLines} lines of code
2. Subtasks must be independently compilable
3. Use partial classes if splitting a single large class
4. Maintain proper dependencies between subtasks (no circular dependencies)
5. First subtask should contain shared definitions (fields, properties, constructors)
6. Subsequent subtasks should contain method implementations

OUTPUT FORMAT (JSON only):
{{
  ""subtasks"": [
    {{
      ""id"": ""original_id_part1"",
      ""description"": ""Part 1: Core definitions and constructors"",
      ""dependencies"": [],
      ""context"": ""Contains fields, properties, constructors"",
      ""namespace"": ""OriginalNamespace"",
      ""expectedTypes"": [""ClassName""],
      ""requiredPackages"": []
    }},
    {{
      ""id"": ""original_id_part2"",
      ""description"": ""Part 2: Main methods"",
      ""dependencies"": [""original_id_part1""],
      ""context"": ""Contains primary method implementations"",
      ""namespace"": ""OriginalNamespace"",
      ""expectedTypes"": [],
      ""requiredPackages"": []
    }}
  ]
}}";

        var userPrompt = $@"Decompose this complex task into {targetSubtaskCount} subtasks:

TASK:
- ID: {task.Id}
- Description: {task.Description}
- Context: {task.Context}
- Namespace: {task.Namespace}
- Expected Types: {string.Join(", ", task.ExpectedTypes)}
- Dependencies: {string.Join(", ", task.Dependencies)}
- Required Packages: {string.Join(", ", task.RequiredPackages)}

Return ONLY valid JSON.";

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
                        throw new InvalidOperationException("OpenAI returned no content for task decomposition.");
                    }
                    await Task.Delay(1000 * (attempt + 1));
                    continue;
                }

                var content = contentParts[0].Text.Trim();
                var response = JsonConvert.DeserializeObject<TaskDecompositionResponse>(content);

                if (response?.Tasks != null && response.Tasks.Any())
                {
                    // Inherit original task's dependencies to first subtask
                    if (response.Tasks.Count > 0 && task.Dependencies != null && task.Dependencies.Any())
                    {
                        response.Tasks[0].Dependencies ??= new List<string>();
                        foreach (var dep in task.Dependencies.Where(d => !response.Tasks[0].Dependencies.Contains(d)))
                        {
                            response.Tasks[0].Dependencies.Add(dep);
                        }
                    }
                    return response.Tasks;
                }

                // Try alternate parsing if Tasks property wasn't populated
                var altResponse = JsonConvert.DeserializeObject<SubtaskResponse>(content);
                if (altResponse?.Subtasks != null && altResponse.Subtasks.Any())
                {
                    // Inherit original task's dependencies to first subtask
                    if (altResponse.Subtasks.Count > 0 && task.Dependencies != null && task.Dependencies.Any())
                    {
                        altResponse.Subtasks[0].Dependencies ??= new List<string>();
                        foreach (var dep in task.Dependencies.Where(d => !altResponse.Subtasks[0].Dependencies.Contains(d)))
                        {
                            altResponse.Subtasks[0].Dependencies.Add(dep);
                        }
                    }
                    return altResponse.Subtasks;
                }
            }
            catch (HttpRequestException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"HTTP error during task decomposition (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (JsonException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"JSON parsing error during task decomposition (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        throw new InvalidOperationException($"Failed to decompose task '{task.Id}' after {MaxRetries} attempts");
    }
}

/// <summary>
/// Alternate response model for subtask decomposition.
/// </summary>
internal class SubtaskResponse
{
    [JsonProperty("subtasks")]
    public List<TaskNode> Subtasks { get; set; } = new();
}
