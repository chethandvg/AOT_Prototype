using AoTEngine.Models;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Service for interacting with OpenAI API.
/// This is the main partial class containing core fields and constructor.
/// </summary>
/// <remarks>
/// This class is split into multiple partial class files for maintainability:
/// - OpenAIService.cs (this file): Core fields, constructor, and initial task decomposition
/// - OpenAIService.CodeGeneration.cs: Code generation and regeneration methods
/// - OpenAIService.Prompts.cs: Prompt generation methods
/// - OpenAIService.ContractExtraction.cs: Type contract extraction methods
/// - OpenAIService.PackageVersions.cs: Package version query methods
/// - OpenAIService.Documentation.cs: Documentation and summary generation
/// - OpenAIService.TaskDecomposition.cs: Complex task decomposition methods
/// </remarks>
public partial class OpenAIService
{
    private readonly ChatClient _chatClient;
    private readonly ChatClient _codeGenChatClient; // Separate client for code generation with codex-max
    private const int MaxRetries = 3;
    
    // Regex for validating semantic version format - compiled once for efficiency
    private static readonly System.Text.RegularExpressions.Regex VersionRegex = 
        new System.Text.RegularExpressions.Regex(@"^\d+\.\d+(\.\d+)?(\.\d+)?(-[\w.]+)?(\+[\w.]+)?$", 
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public OpenAIService(string apiKey, string model = "gpt-4")
    {
        _chatClient = new ChatClient(model, apiKey);
        // Use gpt-4.5-codex-max for code generation phase
        _codeGenChatClient = new ChatClient("gpt-5.2", apiKey);
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
      ""context"": ""Context needed for this task"",
      ""namespace"": ""ProjectName.Feature"",
      ""expectedTypes"": [""ClassName1"", ""InterfaceName1""],
      ""consumedTypes"": {
        ""task2"": [""DependentClass"", ""IService""]
      },
      ""requiredPackages"": [""Newtonsoft.Json"", ""System.Net.Http""]
    }
  ]
}

Rules:
1. Create a DAG (Directed Acyclic Graph) of tasks
2. Each task should be atomic and independently executable
3. Specify dependencies using task IDs
4. Include namespace structure for proper organization (e.g., ProjectName.Services, ProjectName.Models)
5. List expected type names (classes, interfaces, enums) for each task
6. Map consumed types from dependencies with task ID -> type names
7. Specify required NuGet packages or assemblies (if needed beyond System.*)
8. Tasks with no dependencies can run in parallel
9. Ensure no circular dependencies
10. Use meaningful namespace hierarchies to organize code";

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
}
