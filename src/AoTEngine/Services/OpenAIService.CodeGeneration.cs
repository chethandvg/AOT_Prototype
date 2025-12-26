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

                // Use HttpClient for code generation
                var generatedCode = (await CallCodeGenChatCompletionAsync(messages)).Trim();
                
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
    /// Provides the LLM with original intent, failed code, error log, and suggestions for targeted repairs.
    /// Filters out namespace and type not found errors as these will be resolved in batch validation.
    /// </summary>
    /// <param name="task">The task to regenerate code for.</param>
    /// <param name="validationResult">Validation errors and warnings.</param>
    /// <param name="suggestions">Optional list of suggestions for fixing the code.</param>
    /// <param name="existingTypeCode">Optional code from existing types that should be reused instead of regenerated.</param>
    public async Task<string> RegenerateCodeWithErrorsAsync(
        TaskNode task, 
        ValidationResult validationResult,
        List<string>? suggestions = null,
        string? existingTypeCode = null)
    {
        // Filter out namespace and type-related errors that will be resolved in batch validation
        var filteredErrors = FilterSpecificErrors(validationResult.Errors);
        
        // If all errors were filtered out, return the original code (errors will be fixed in batch validation)
        if (!filteredErrors.Any() && suggestions == null && existingTypeCode == null)
        {
            Console.WriteLine($"   ℹ️  All errors for task {task.Id} are namespace/type-related and will be resolved in batch validation");
            return task.GeneratedCode;
        }
        
        // Generate automatic suggestions based on error patterns if none provided
        var allSuggestions = suggestions?.Any() == true 
            ? suggestions 
            : GenerateSuggestionsFromErrors(filteredErrors);
        
        var errorsText = string.Join("\n", filteredErrors);
        var warningsText = string.Join("\n", validationResult.Warnings);
        var suggestionsText = allSuggestions.Any() ? string.Join("\n", allSuggestions.Select((s, i) => $"{i + 1}. {s}")) : "";

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Use "Code Repair Expert" pattern with original intent, failed code, error log, and suggestions
                var systemPrompt = GetCodeRegenerationSystemPrompt(attempt);
                var userPrompt = GetCodeRegenerationUserPrompt(
                    task.Description,
                    task.Namespace,
                    task.ExpectedTypes,
                    task.GeneratedCode, 
                    errorsText, 
                    warningsText,
                    suggestionsText,
                    existingTypeCode,
                    attempt);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                // Use HttpClient for code regeneration
                var regeneratedCode = (await CallCodeGenChatCompletionAsync(messages)).Trim();
                
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
    /// Generates suggestions based on common error patterns.
    /// </summary>
    private List<string> GenerateSuggestionsFromErrors(List<string> errors)
    {
        var suggestions = new List<string>();
        
        foreach (var error in errors)
        {
            var errorLower = error.ToLowerInvariant();
            
            // Suggestion for missing members
            if (errorLower.Contains("does not contain a definition for"))
            {
                var memberMatch = System.Text.RegularExpressions.Regex.Match(error, @"'([^']+)'.*does not contain a definition for\s+'([^']+)'");
                if (memberMatch.Success)
                {
                    suggestions.Add($"Add missing member '{memberMatch.Groups[2].Value}' to type '{memberMatch.Groups[1].Value}'");
                }
            }
            
            // Suggestion for interface implementation
            if (errorLower.Contains("does not implement interface member"))
            {
                var implMatch = System.Text.RegularExpressions.Regex.Match(error, @"'([^']+)'.*does not implement interface member\s+'([^']+)'");
                if (implMatch.Success)
                {
                    suggestions.Add($"Implement interface member '{implMatch.Groups[2].Value}' in '{implMatch.Groups[1].Value}'");
                }
            }
            
            // Suggestion for type mismatch
            if (errorLower.Contains("cannot convert") || errorLower.Contains("cannot implicitly convert"))
            {
                var convertMatch = System.Text.RegularExpressions.Regex.Match(error, @"cannot (?:implicitly )?convert.*'([^']+)'.*to.*'([^']+)'");
                if (convertMatch.Success)
                {
                    suggestions.Add($"Convert type '{convertMatch.Groups[1].Value}' to '{convertMatch.Groups[2].Value}' or change the expected type");
                }
            }
            
            // Suggestion for missing using directive
            if (errorLower.Contains("are you missing a using directive"))
            {
                suggestions.Add("Add the required using directive at the top of the file");
            }
            
            // Suggestion for accessibility issues
            if (errorLower.Contains("is inaccessible due to its protection level"))
            {
                suggestions.Add("Change the access modifier to public or internal, or use a public accessor method");
            }
            
            // Suggestion for abstract member implementation
            if (errorLower.Contains("does not implement inherited abstract member"))
            {
                var abstractMatch = System.Text.RegularExpressions.Regex.Match(error, @"'([^']+)'.*does not implement inherited abstract member\s+'([^']+)'");
                if (abstractMatch.Success)
                {
                    suggestions.Add($"Implement abstract member '{abstractMatch.Groups[2].Value}' with override keyword");
                }
            }
            
            // Suggestion for wrong number of arguments
            // Use explicit parentheses for operator precedence clarity
            if (errorLower.Contains("no overload for method") || (errorLower.Contains("takes") && errorLower.Contains("arguments")))
            {
                suggestions.Add("Check the method signature and provide the correct number and types of arguments");
            }
        }
        
        // Remove duplicates and return
        return suggestions.Distinct().ToList();
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
