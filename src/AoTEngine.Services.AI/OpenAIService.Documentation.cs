using AoTEngine.Models;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing documentation and summary generation methods.
/// </summary>
public partial class OpenAIService
{
    /// <summary>
    /// Generates a structured summary for a task's generated code.
    /// </summary>
    /// <param name="task">The task to generate a summary for.</param>
    /// <param name="dependencyTasks">Dictionary of dependency tasks for context.</param>
    /// <returns>A TaskSummaryInfo containing the structured summary, or null if generation fails.</returns>
    public async Task<TaskSummaryInfo?> GenerateTaskSummaryAsync(
        TaskNode task, 
        Dictionary<string, TaskNode> dependencyTasks)
    {
        var systemPrompt = @"You are an expert code documenter. Analyze the provided C# code and generate a structured summary.
Your output must be valid JSON following this structure:
{
  ""purpose"": ""A concise one-sentence description of what this code does"",
  ""key_behaviors"": [""List of key behaviors/features implemented""],
  ""edge_cases"": [""List of edge cases handled""]
}

Rules:
1. Be concise but informative
2. Focus on what the code does, not how it's structured
3. Identify actual edge cases handled in the code
4. Return ONLY valid JSON, no explanations";

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine($"Task ID: {task.Id}");
        contextBuilder.AppendLine($"Task Description: {task.Description}");
        
        if (!string.IsNullOrEmpty(task.Namespace))
        {
            contextBuilder.AppendLine($"Namespace: {task.Namespace}");
        }
        
        if (task.ExpectedTypes.Any())
        {
            contextBuilder.AppendLine($"Types: {string.Join(", ", task.ExpectedTypes)}");
        }
        
        if (task.Dependencies.Any())
        {
            contextBuilder.AppendLine($"Dependencies: {string.Join(", ", task.Dependencies)}");
            
            // Include dependency summaries if available
            foreach (var depId in task.Dependencies.Where(d => dependencyTasks.ContainsKey(d)))
            {
                var depTask = dependencyTasks[depId];
                if (!string.IsNullOrEmpty(depTask.Summary))
                {
                    contextBuilder.AppendLine($"  {depId} summary: {depTask.Summary}");
                }
            }
        }
        
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Code:");
        contextBuilder.AppendLine(task.GeneratedCode);

        var userPrompt = $@"Analyze this code and generate a structured summary:

{contextBuilder}

Return ONLY valid JSON with the structure specified.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        try
        {
            var completion = await _chatClient.CompleteChatAsync(messages);
            var contentParts = completion.Value.Content;
            
            if (contentParts == null || contentParts.Count == 0)
            {
                return null;
            }

            var firstPart = contentParts[0];
            if (firstPart?.Text == null)
            {
                return null;
            }

            var content = firstPart.Text.Trim();
            
            // Parse the JSON response with specific error handling
            try
            {
                var response = JsonConvert.DeserializeObject<TaskSummaryInfo>(content);
                if (response == null)
                {
                    Console.WriteLine($"   ⚠️  JSON for task {task.Id} deserialized to null");
                }
                return response;
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"   ⚠️  Failed to parse JSON summary for task {task.Id}: {jsonEx.Message}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Error generating summary for task {task.Id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generates a high-level architecture summary for the project.
    /// </summary>
    /// <param name="tasks">All tasks in the project.</param>
    /// <param name="originalRequest">The original user request.</param>
    /// <param name="description">Description from decomposition.</param>
    /// <returns>A markdown-formatted architecture summary.</returns>
    public async Task<string> GenerateArchitectureSummaryAsync(
        List<TaskNode> tasks, 
        string originalRequest, 
        string description)
    {
        var systemPrompt = @"You are an expert software architect. Generate a concise high-level architecture summary for the code project.
Focus on:
1. Overall structure and design patterns used
2. How components interact
3. Key design decisions
4. Main entry points and flow

Keep the summary concise (2-3 paragraphs) and use markdown formatting.";

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine($"Original Request: {originalRequest}");
        contextBuilder.AppendLine($"Description: {description}");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Tasks and their summaries:");
        
        foreach (var task in tasks.OrderBy(t => t.Id))
        {
            var deps = task.Dependencies.Any() ? $" [depends on: {string.Join(", ", task.Dependencies)}]" : "";
            var summary = !string.IsNullOrEmpty(task.Summary) ? task.Summary : task.Description;
            contextBuilder.AppendLine($"- {task.Id}: {summary}{deps}");
            
            if (task.ExpectedTypes.Any())
            {
                contextBuilder.AppendLine($"  Types: {string.Join(", ", task.ExpectedTypes)}");
            }
        }

        var userPrompt = $@"Generate a high-level architecture summary for this project:

{contextBuilder}

Return a concise markdown-formatted summary.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        try
        {
            var completion = await _chatClient.CompleteChatAsync(messages);
            var contentParts = completion.Value.Content;
            
            if (contentParts == null || contentParts.Count == 0)
            {
                return description;
            }

            var firstPart = contentParts[0];
            if (firstPart?.Text == null)
            {
                return description;
            }

            return firstPart.Text.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Error generating architecture summary: {ex.Message}");
            return description;
        }
    }
}

/// <summary>
/// Structured summary information for a task.
/// </summary>
public class TaskSummaryInfo
{
    [JsonProperty("purpose")]
    public string Purpose { get; set; } = string.Empty;
    
    [JsonProperty("key_behaviors")]
    public List<string> KeyBehaviors { get; set; } = new();
    
    [JsonProperty("edge_cases")]
    public List<string> EdgeCases { get; set; } = new();
}
