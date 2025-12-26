using AoTEngine.Models;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing code generation and regeneration methods.
/// </summary>
public partial class OpenAIService
{
    /// <summary>
    /// Generates code for a specific task.
    /// </summary>
    public async Task<string> GenerateCodeAsync(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine($"Task: {task.Description}");
        contextBuilder.AppendLine($"Context: {task.Context}");

        if (!string.IsNullOrEmpty(task.Namespace))
        {
            contextBuilder.AppendLine($"Target Namespace: {task.Namespace}");
        }

        if (task.ExpectedTypes.Any())
        {
            contextBuilder.AppendLine($"Expected Types to Generate: {string.Join(", ", task.ExpectedTypes)}");
        }

        // Include outputs from dependent tasks
        if (task.Dependencies.Any())
        {
            contextBuilder.AppendLine("\nDependent task outputs:");
            foreach (var depId in task.Dependencies.Where(depId => completedTasks.ContainsKey(depId)))
            {
                var depTask = completedTasks[depId];
                
                // Extract and use type contract (interface/API signatures)
                var contract = ExtractTypeContract(depTask.GeneratedCode);
                
                if (!string.IsNullOrWhiteSpace(contract))
                {
                    contextBuilder.AppendLine($"\n{depId} Type Contract (use these types):");
                    contextBuilder.AppendLine(contract);
                }
                
                // Include full code only for small snippets (< 500 chars)
                if (depTask.GeneratedCode.Length < 500)
                {
                    contextBuilder.AppendLine($"\n{depId} Full Implementation:");
                    contextBuilder.AppendLine(depTask.GeneratedCode);
                }
            }
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Use enhanced prompt on the 3rd attempt
                var systemPrompt = GetCodeGenerationSystemPrompt(attempt);
                var userPrompt = GetCodeGenerationUserPrompt(contextBuilder.ToString(), attempt, task);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                // Use gpt-4.5-codex-max for code generation
                var completion = await _codeGenChatClient.CompleteChatAsync(messages);
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

                var generatedCode = contentParts[0].Text.Trim();
                
                // Extract and store type contract after generation
                task.TypeContract = ExtractTypeContract(generatedCode);
                
                return generatedCode;
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
    /// Regenerates code with validation errors as feedback using the "Code Repair Expert" pattern.
    /// Provides the LLM with original intent, failed code, and error log for targeted repairs.
    /// Filters out namespace and type not found errors as these will be resolved in batch validation.
    /// </summary>
    public async Task<string> RegenerateCodeWithErrorsAsync(TaskNode task, ValidationResult validationResult)
    {
        // Filter out namespace and type-related errors that will be resolved in batch validation
        var filteredErrors = FilterSpecificErrors(validationResult.Errors);
        
        // If all errors were filtered out, return the original code (errors will be fixed in batch validation)
        if (!filteredErrors.Any())
        {
            Console.WriteLine($"   ℹ️  All errors for task {task.Id} are namespace/type-related and will be resolved in batch validation");
            return task.GeneratedCode;
        }
        
        var errorsText = string.Join("\n", filteredErrors);
        var warningsText = string.Join("\n", validationResult.Warnings);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Use "Code Repair Expert" pattern with original intent, failed code, and error log
                var systemPrompt = GetCodeRegenerationSystemPrompt(attempt);
                var userPrompt = GetCodeRegenerationUserPrompt(
                    task.Description,
                    task.Namespace,
                    task.ExpectedTypes,
                    task.GeneratedCode, 
                    errorsText, 
                    warningsText, 
                    attempt);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                // Use gpt-4.5-codex-max for code regeneration
                var completion = await _codeGenChatClient.CompleteChatAsync(messages);
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

                var regeneratedCode = contentParts[0].Text.Trim();
                
                // Update type contract after regeneration
                task.TypeContract = ExtractTypeContract(regeneratedCode);
                
                return regeneratedCode;
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

    /// <summary>
    /// Filters out namespace and type-related errors that will be resolved during batch validation.
    /// </summary>
    private List<string> FilterSpecificErrors(List<string> errors)
    {
        var filteredErrors = new List<string>();
        
        foreach (var error in errors)
        {
            var errorLower = error.ToLowerInvariant();
            
            // Skip errors related to missing namespaces
            if (errorLower.Contains("namespace") && 
                (errorLower.Contains("could not be found") || 
                 errorLower.Contains("does not exist") ||
                 errorLower.Contains("not found")))
            {
                continue;
            }
            
            // Skip errors related to missing types
            if ((errorLower.Contains("type") || 
                 errorLower.Contains("class") || 
                 errorLower.Contains("interface") ||
                 errorLower.Contains("struct") ||
                 errorLower.Contains("enum") ||
                 errorLower.Contains("record")) && 
                (errorLower.Contains("could not be found") || 
                 errorLower.Contains("does not exist") ||
                 errorLower.Contains("not found") ||
                 errorLower.Contains("missing")))
            {
                continue;
            }
            
            // Skip "The type or namespace name" errors
            if (errorLower.Contains("the type or namespace name"))
            {
                continue;
            }
            
            // Skip "using directive" errors for missing namespaces
            if (errorLower.Contains("using") && errorLower.Contains("directive"))
            {
                continue;
            }
            
            // Keep all other errors
            filteredErrors.Add(error);
        }
        
        return filteredErrors;
    }
}
