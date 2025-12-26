using AoTEngine.Models;
using Newtonsoft.Json;
using OpenAI.Chat;
using System.Net.Http.Headers;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// OpenAI API response models for Chat Completion endpoint.
/// </summary>
internal class ChatCompletionResponse
{
    [JsonProperty("choices")]
    public List<ChatCompletionChoice>? Choices { get; set; }

    [JsonProperty("error")]
    public ChatCompletionError? Error { get; set; }
}

internal class ChatCompletionChoice
{
    [JsonProperty("message")]
    public ChatCompletionMessage? Message { get; set; }
}

internal class ChatCompletionMessage
{
    [JsonProperty("content")]
    public string? Content { get; set; }
}

internal class ChatCompletionError
{
    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("code")]
    public string? Code { get; set; }
}

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
public partial class OpenAIService : IDisposable
{
    private readonly ChatClient _chatClient;
    // Use static HttpClient to avoid socket exhaustion and DNS issues.
    // HttpClient for code generation instead of OpenAI SDK to allow direct control over
    // the HTTP request/response for the gpt-5.1-codex model, which requires specific
    // settings and payload structure for optimal code generation performance.
    private static readonly HttpClient _sharedCodeGenHttpClient = new HttpClient
    {
        BaseAddress = new Uri("https://api.openai.com/v1/")
    };
    private readonly string _apiKey;
    private readonly string _codeGenModel = "gpt-5.1-codex";
    private const int MaxRetries = 3;
    private bool _disposed = false;
    
    // Regex for validating semantic version format - compiled once for efficiency
    private static readonly System.Text.RegularExpressions.Regex VersionRegex = 
        new System.Text.RegularExpressions.Regex(@"^\d+\.\d+(\.\d+)?(\.\d+)?(-[\w.]+)?(\+[\w.]+)?$", 
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public OpenAIService(string apiKey, string model = "gpt-5.1")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key cannot be null or empty.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _chatClient = new ChatClient(model, apiKey);
    }

    /// <summary>
    /// Helper method to call OpenAI Chat Completion API using HttpClient for code generation.
    /// </summary>
    private async Task<string> CallCodeGenChatCompletionAsync(List<ChatMessage> messages)
    {
        if (messages == null)
        {
            throw new ArgumentNullException(nameof(messages));
        }

        if (messages.Count == 0)
        {
            throw new ArgumentException("Messages collection cannot be empty.", nameof(messages));
        }

        try
        {
            var requestBody = new
            {
                model = _codeGenModel,
                messages = messages.Select(m => new
                {
                    role = m switch
                    {
                        SystemChatMessage => "system",
                        UserChatMessage => "user",
                        AssistantChatMessage => "assistant",
                        _ => "user"
                    },
                    content = m switch
                    {
                        SystemChatMessage sys when sys.Content.Count > 0 => sys.Content[0].Text,
                        UserChatMessage usr when usr.Content.Count > 0 => usr.Content[0].Text,
                        AssistantChatMessage ast when ast.Content.Count > 0 => ast.Content[0].Text,
                        _ => throw new InvalidOperationException("Chat message has no content or is in an unsupported format.")
                    }
                }).ToList()
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            requestMessage.Content = httpContent;
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _sharedCodeGenHttpClient.SendAsync(requestMessage);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = $"OpenAI API request failed with status code {(int)response.StatusCode} ({response.StatusCode}).";

                try
                {
                    var errorResponse = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseBody);
                    var apiError = errorResponse?.Error;
                    if (apiError != null)
                    {
                        var detailsBuilder = new StringBuilder();
                        if (!string.IsNullOrWhiteSpace(apiError.Message))
                        {
                            detailsBuilder.Append(apiError.Message);
                        }
                        if (!string.IsNullOrWhiteSpace(apiError.Type))
                        {
                            if (detailsBuilder.Length > 0) detailsBuilder.Append(' ');
                            detailsBuilder.Append($"(type: {apiError.Type})");
                        }
                        if (!string.IsNullOrWhiteSpace(apiError.Code))
                        {
                            if (detailsBuilder.Length > 0) detailsBuilder.Append(' ');
                            detailsBuilder.Append($"(code: {apiError.Code})");
                        }

                        if (detailsBuilder.Length > 0)
                        {
                            errorMessage += " Details: " + detailsBuilder.ToString();
                        }
                    }
                }
                catch (JsonException)
                {
                    // If the error body is not valid JSON, fall back to the generic message.
                }

                throw new HttpRequestException(errorMessage);
            }

            var result = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseBody);

            if (result == null || result.Choices == null || result.Choices.Count == 0)
            {
                throw new InvalidOperationException("OpenAI API returned no choices in response.");
            }

            string? content = result.Choices[0]?.Message?.Content;
            return content ?? throw new InvalidOperationException("OpenAI API returned null content.");
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException("Error serializing or deserializing OpenAI code generation request/response.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new HttpRequestException("Received an invalid OpenAI code generation response.", ex);
        }
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

    /// <summary>
    /// Disposes the resources used by the service.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                (_chatClient as IDisposable)?.Dispose();
            }
            _disposed = true;
        }
    }
}
