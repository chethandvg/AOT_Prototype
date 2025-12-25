using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Core;

/// <summary>
/// Partial class containing task regeneration methods.
/// </summary>
public partial class ParallelExecutionEngine
{
    /// <summary>
    /// Regenerates specific tasks with targeted error feedback.
    /// </summary>
    private async Task RegenerateSpecificTasksAsync(
        List<TaskNode> tasksToRegenerate,
        ValidationResult validationResult,
        Dictionary<string, TaskNode> completedTasks)
    {
        foreach (var task in tasksToRegenerate)
        {
            try
            {
                Console.WriteLine($"   🔄 Regenerating task {task.Id}...");
                
                // Create task-specific validation result with only relevant errors
                var taskSpecificErrors = new List<string>();
                
                if (task.ValidationErrors != null && task.ValidationErrors.Any())
                {
                    // Use the task-specific errors identified earlier
                    taskSpecificErrors = task.ValidationErrors;
                }
                else
                {
                    // Extract errors that might be related to this task
                    taskSpecificErrors = ExtractTaskRelevantErrors(task, validationResult.Errors);
                }
                
                if (!taskSpecificErrors.Any())
                {
                    // If no specific errors found, provide general context
                    taskSpecificErrors.Add(
                        "This task's code caused validation errors when combined with other tasks. " +
                        "Review type definitions, namespaces, and ensure all references are correct.");
                }
                
                var modifiedValidationResult = new ValidationResult
                {
                    IsValid = false,
                    Errors = taskSpecificErrors
                };
                
                // Add helpful context
                modifiedValidationResult.Errors.Insert(0, 
                    $"[BATCH VALIDATION ERROR - Task {task.Id}] " +
                    $"The following errors occurred when this code was combined with other tasks:");
                
                Console.WriteLine($"      Providing {taskSpecificErrors.Count - 1} specific error(s) as feedback");
                
                task.GeneratedCode = await _openAIService.RegenerateCodeWithErrorsAsync(task, modifiedValidationResult);
                task.RetryCount++;
                Console.WriteLine($"   ✓ Regenerated code for task {task.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Failed to regenerate task {task.Id}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Extracts errors from the validation result that are relevant to a specific task.
    /// </summary>
    private List<string> ExtractTaskRelevantErrors(TaskNode task, List<string> allErrors)
    {
        var relevantErrors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(task.GeneratedCode))
            return relevantErrors;
        
        // Extract type/class names defined in this task
        var definedTypes = new HashSet<string>();
        var typeMatches = System.Text.RegularExpressions.Regex.Matches(
            task.GeneratedCode,
            @"(?:class|interface|enum|struct|record)\s+([A-Z][a-zA-Z0-9_]+)");
        
        foreach (System.Text.RegularExpressions.Match match in typeMatches)
        {
            definedTypes.Add(match.Groups[1].Value);
        }
        
        // Extract method names defined in this task
        var methodMatches = System.Text.RegularExpressions.Regex.Matches(
            task.GeneratedCode,
            @"(?:public|private|protected|internal|static).*\s+([A-Z][a-zA-Z0-9_]+)\s*\(");
        
        foreach (System.Text.RegularExpressions.Match match in methodMatches)
        {
            definedTypes.Add(match.Groups[1].Value);
        }
        
        // Find errors that mention these types
        foreach (var error in allErrors)
        {
            foreach (var type in definedTypes)
            {
                if (error.Contains(type))
                {
                    relevantErrors.Add(error);
                    break;
                }
            }
        }
        
        // Also check for namespace issues
        var namespaceMatch = System.Text.RegularExpressions.Regex.Match(
            task.GeneratedCode,
            @"namespace\s+([A-Za-z0-9_.]+)");
        
        if (namespaceMatch.Success)
        {
            var taskNamespace = namespaceMatch.Groups[1].Value;
            foreach (var error in allErrors)
            {
                if (error.Contains(taskNamespace) && !relevantErrors.Contains(error))
                {
                    relevantErrors.Add(error);
                }
            }
        }
        
        return relevantErrors;
    }

    /// <summary>
    /// Attempts to regenerate tasks that are causing validation errors.
    /// </summary>
    private async Task RegenerateProblematicTasksAsync(
        List<TaskNode> tasks, 
        ValidationResult validationResult, 
        Dictionary<string, TaskNode> completedTasks)
    {
        Console.WriteLine("\n🔄 Attempting to fix validation errors by regenerating problematic code...");
        
        // For now, regenerate all tasks with the error context
        // In a more sophisticated version, we could identify specific problematic tasks
        var errorContext = string.Join("\n", validationResult.Errors);
        
        foreach (var task in tasks.Where(t => !string.IsNullOrWhiteSpace(t.GeneratedCode)))
        {
            try
            {
                var modifiedValidationResult = new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string>(validationResult.Errors)
                };
                
                task.GeneratedCode = await _openAIService.RegenerateCodeWithErrorsAsync(task, modifiedValidationResult);
                task.RetryCount++;
                Console.WriteLine($"   ✓ Regenerated code for task {task.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Failed to regenerate task {task.Id}: {ex.Message}");
            }
        }
    }
}
