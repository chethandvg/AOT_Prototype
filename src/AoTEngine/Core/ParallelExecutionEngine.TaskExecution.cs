using AoTEngine.Models;
using AoTEngine.Services;
using Newtonsoft.Json;

namespace AoTEngine.Core;

/// <summary>
/// Partial class containing individual task execution methods.
/// </summary>
public partial class ParallelExecutionEngine
{
    /// <summary>
    /// Executes all tasks in the DAG, running independent tasks in parallel.
    /// This is the original version that validates each task individually.
    /// </summary>
    public async Task<List<TaskNode>> ExecuteTasksAsync(List<TaskNode> tasks)
    {
        // Handle all uncertainties upfront before execution
        await _userInteractionService.HandleMultipleTaskUncertaintiesAsync(tasks);
        
        var completedTasks = new Dictionary<string, TaskNode>();

        while (completedTasks.Count < tasks.Count)
        {
            // Find tasks that are ready to execute (all dependencies completed)
            var readyTasks = tasks
                .Where(t => !t.IsCompleted && AreAllDependenciesMet(t, completedTasks))
                .ToList();

            if (!readyTasks.Any())
            {
                // Check for circular dependencies or other issues
                var incompleteTasks = tasks.Where(t => !t.IsCompleted).ToList();
                if (incompleteTasks.Any())
                {
                    throw new InvalidOperationException(
                        $"Deadlock detected. Tasks {string.Join(", ", incompleteTasks.Select(t => t.Id))} cannot be completed. " +
                        "Possible circular dependencies or missing tasks.");
                }
                break;
            }

            // Execute ready tasks in parallel
            var executionTasks = readyTasks.Select(task => ExecuteTaskWithValidationAsync(task, completedTasks));
            var results = await Task.WhenAll(executionTasks);

            // Mark completed tasks
            foreach (var task in results)
            {
                task.IsCompleted = true;
                completedTasks[task.Id] = task;
            }

            Console.WriteLine($"Completed {completedTasks.Count}/{tasks.Count} tasks");
            
            // Save checkpoint after each batch of tasks
            await SaveCheckpointAsync(tasks, completedTasks);
        }

        // Save final checkpoint
        await SaveCheckpointAsync(tasks, completedTasks, "completed");

        return tasks;
    }

    /// <summary>
    /// Executes a single task with validation and retry logic.
    /// </summary>
    private async Task<TaskNode> ExecuteTaskWithValidationAsync(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        Console.WriteLine($"Executing task: {task.Id} - {task.Description}");

        // Validate dependencies first (if task has consumed types)
        if (task.ConsumedTypes != null && task.ConsumedTypes.Any())
        {
            var dependencyValidation = _validatorService.ValidateDependencies(task, completedTasks);
            if (!dependencyValidation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Task {task.Id} dependency validation failed: {string.Join(", ", dependencyValidation.Errors)}");
            }
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Generate code for the task
                if (string.IsNullOrEmpty(task.GeneratedCode))
                {
                    task.GeneratedCode = await _openAIService.GenerateCodeAsync(task, completedTasks);
                    Console.WriteLine($"Generated code for task {task.Id} (attempt {attempt + 1})");
                }

                // Validate the generated code
                var validationResult = await _validatorService.ValidateCodeAsync(task.GeneratedCode);
                
                // Also run linting
                var lintResult = _validatorService.LintCode(task.GeneratedCode);
                validationResult.Warnings.AddRange(lintResult.Warnings);

                if (validationResult.IsValid)
                {
                    task.IsValidated = true;
                    task.ValidationErrors.Clear();
                    task.ValidationAttemptCount = attempt + 1;
                    Console.WriteLine($"Task {task.Id} validated successfully");
                    
                    // Generate summary after successful validation
                    await GenerateTaskSummaryAsync(task, completedTasks);
                    
                    break;
                }
                else
                {
                    task.ValidationErrors = validationResult.Errors;
                    Console.WriteLine($"Task {task.Id} validation failed (attempt {attempt + 1}): {string.Join(", ", validationResult.Errors)}");

                    if (attempt < MaxRetries - 1)
                    {
                        // Re-prompt with errors
                        task.GeneratedCode = await _openAIService.RegenerateCodeWithErrorsAsync(task, validationResult);
                        task.RetryCount++;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Task {task.Id} failed validation after {MaxRetries} attempts. Errors: {string.Join(", ", validationResult.Errors)}");
                    }
                }
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Console.WriteLine($"HTTP error executing task {task.Id} (attempt {attempt + 1}): {ex.Message}");
                task.RetryCount++;
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (JsonException ex) when (attempt < MaxRetries - 1)
            {
                Console.WriteLine($"JSON parsing error executing task {task.Id} (attempt {attempt + 1}): {ex.Message}");
                task.RetryCount++;
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (Exception ex) when (attempt < MaxRetries - 1)
            {
                Console.WriteLine($"Error executing task {task.Id} (attempt {attempt + 1}): {ex.Message}");
                task.RetryCount++;
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        // Verify task was validated successfully
        if (!task.IsValidated)
        {
            throw new InvalidOperationException(
                $"Task {task.Id} failed to validate after {MaxRetries} attempts.");
        }

        return task;
    }

    /// <summary>
    /// Generates code for a single task without validation.
    /// </summary>
    private async Task<TaskNode> GenerateCodeForTaskAsync(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        Console.WriteLine($"Generating code for task: {task.Id} - {task.Description}");

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Generate code for the task
                task.GeneratedCode = await _openAIService.GenerateCodeAsync(task, completedTasks);
                Console.WriteLine($"✓ Generated code for task {task.Id}");
                return task;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Console.WriteLine($"HTTP error generating code for task {task.Id} (attempt {attempt + 1}): {ex.Message}");
                task.RetryCount++;
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (JsonException ex) when (attempt < MaxRetries - 1)
            {
                Console.WriteLine($"JSON parsing error for task {task.Id} (attempt {attempt + 1}): {ex.Message}");
                task.RetryCount++;
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (Exception ex) when (attempt < MaxRetries - 1)
            {
                Console.WriteLine($"Error generating code for task {task.Id} (attempt {attempt + 1}): {ex.Message}");
                task.RetryCount++;
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        throw new InvalidOperationException($"Failed to generate code for task {task.Id} after {MaxRetries} attempts.");
    }
}
