using AoTEngine.Models;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Service for interacting with OpenAI API.
/// </summary>
public class OpenAIService
{
    private readonly ChatClient _chatClient;
    private const int MaxRetries = 3;

    public OpenAIService(string apiKey, string model = "gpt-4")
    {
        _chatClient = new ChatClient(model, apiKey);
    }

    /// <summary>
    /// Decomposes a user request into atomic subtasks with dependencies.
    /// </summary>
    public async Task<TaskDecompositionResponse> DecomposeTaskAsync(TaskDecompositionRequest request)
    {
        var systemPrompt = @"You are an expert at decomposing complex programming tasks into atomic subtasks.
Your output must be valid JSON following this structure:
{
  ""description"": ""Overall description"",
  ""tasks"": [
    {
      ""id"": ""task1"",
      ""description"": ""Task description"",
      ""dependencies"": [],
      ""context"": ""Context needed for this task""
    }
  ]
}

Rules:
1. Create a DAG (Directed Acyclic Graph) of tasks
2. Each task should be atomic and independently executable
3. Specify dependencies using task IDs
4. Include only necessary context for each task
5. Tasks with no dependencies can run in parallel
6. Ensure no circular dependencies";

        var userPrompt = $@"Decompose this request into atomic subtasks:

Request: {request.OriginalRequest}
Context: {request.Context}

Return ONLY valid JSON with the structure specified.";

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
                        throw new InvalidOperationException("OpenAI chat completion returned no content.");
                    }
                    await Task.Delay(1000 * (attempt + 1));
                    continue;
                }

                var content = contentParts[0].Text;

                // Try to parse the JSON response
                var response = JsonConvert.DeserializeObject<TaskDecompositionResponse>(content);
                if (response?.Tasks != null && response.Tasks.Count > 0)
                {
                    return response;
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

        throw new InvalidOperationException("Failed to decompose task after multiple attempts");
    }

    /// <summary>
    /// Generates code for a specific task.
    /// </summary>
    public async Task<string> GenerateCodeAsync(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine($"Task: {task.Description}");
        contextBuilder.AppendLine($"Context: {task.Context}");

        // Include outputs from dependent tasks
        if (task.Dependencies.Any())
        {
            contextBuilder.AppendLine("\nDependent task outputs:");
            foreach (var depId in task.Dependencies.Where(depId => completedTasks.ContainsKey(depId)))
            {
                var depTask = completedTasks[depId];
                contextBuilder.AppendLine($"\n{depId}:");
                contextBuilder.AppendLine(depTask.GeneratedCode);
            }
        }

        var systemPrompt = @"You are an expert C# programmer. Generate clean, efficient, and well-documented C# code.
Include necessary using statements and create complete, runnable code snippets.
Follow best practices and ensure code is production-ready.";

        var userPrompt = $@"Generate C# code for the following:

{contextBuilder}

Return ONLY the C# code without any markdown formatting or explanations.";

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
                        throw new InvalidOperationException("OpenAI chat completion returned no content.");
                    }
                    await Task.Delay(1000 * (attempt + 1));
                    continue;
                }

                return contentParts[0].Text.Trim();
            }
            catch (HttpRequestException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"HTTP error during code generation (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        throw new InvalidOperationException("Failed to generate code after multiple attempts");
    }

    /// <summary>
    /// Regenerates code with validation errors as feedback.
    /// </summary>
    public async Task<string> RegenerateCodeWithErrorsAsync(TaskNode task, ValidationResult validationResult)
    {
        var systemPrompt = @"You are an expert C# programmer. Fix the code based on the validation errors provided.
Generate clean, efficient, and well-documented C# code.
Include necessary using statements and create complete, runnable code snippets.";

        var errorsText = string.Join("\n", validationResult.Errors);
        var warningsText = string.Join("\n", validationResult.Warnings);

        var userPrompt = $@"The following code has validation errors:

Code:
{task.GeneratedCode}

Errors:
{errorsText}

Warnings:
{warningsText}

Fix the code and return ONLY the corrected C# code without any markdown formatting or explanations.";

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
                        throw new InvalidOperationException("OpenAI chat completion returned no content.");
                    }
                    await Task.Delay(1000 * (attempt + 1));
                    continue;
                }

                return contentParts[0].Text.Trim();
            }
            catch (HttpRequestException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"HTTP error during code regeneration (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        throw new InvalidOperationException("Failed to regenerate code after multiple attempts");
    }
}
