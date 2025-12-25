using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Core;

/// <summary>
/// Partial class containing hybrid validation execution methods.
/// </summary>
public partial class ParallelExecutionEngine
{
    /// <summary>
    /// Executes all tasks in the DAG, running independent tasks in parallel.
    /// This version uses hybrid validation: validates each task individually first, then validates combined code.
    /// Uses ProjectBuildService to build and validate at the specified output directory if available.
    /// </summary>
    public async Task<List<TaskNode>> ExecuteTasksWithHybridValidationAsync(List<TaskNode> tasks)
    {
        // Handle all uncertainties upfront before execution
        await _userInteractionService.HandleMultipleTaskUncertaintiesAsync(tasks);
        
        Console.WriteLine("\n🔧 Generating and validating code for all tasks...");
        
        // Step 1: Generate and validate code for all tasks individually (respecting dependencies)
        var completedTasks = new Dictionary<string, TaskNode>();
        
        while (completedTasks.Count < tasks.Count)
        {
            // Find tasks that are ready to generate (all dependencies have generated code)
            var readyTasks = tasks
                .Where(t => !t.IsCompleted && AreAllDependenciesGenerated(t, completedTasks))
                .ToList();

            if (!readyTasks.Any())
            {
                var incompleteTasks = tasks.Where(t => !t.IsCompleted).ToList();
                if (incompleteTasks.Any())
                {
                    Console.WriteLine("\n🔍  Deadlock detected. Analyzing dependencies...");
                    foreach (var incompleteTask in incompleteTasks)
                    {
                        Console.WriteLine($"   Task: {incompleteTask.Id}");
                        Console.WriteLine($"   Dependencies: {string.Join(", ", incompleteTask.Dependencies)}");
                        var missingDeps = incompleteTask.Dependencies.Where(depId => !completedTasks.ContainsKey(depId)).ToList();
                        if (missingDeps.Any())
                        {
                            Console.WriteLine($"   Missing dependencies: {string.Join(", ", missingDeps)}");
                        }
                    }
                    
                    throw new InvalidOperationException(
                        $"Deadlock detected. Tasks {string.Join(", ", incompleteTasks.Select(t => t.Id))} cannot be completed. " +
                        "Possible circular dependencies or missing tasks.");
                }
                break;
            }

            // Generate and validate ready tasks in parallel
            var generationTasks = readyTasks.Select(task => GenerateAndValidateTaskAsync(task, completedTasks));
            var results = await Task.WhenAll(generationTasks);

            // Mark completed tasks
            foreach (var task in results)
            {
                task.IsCompleted = true;
                completedTasks[task.Id] = task;
            }

            Console.WriteLine($"Generated and validated {completedTasks.Count}/{tasks.Count} tasks");
        }

        // Step 2: Combine all generated code (still needed for Roslyn validation fallback)
        Console.WriteLine("\n📋 Preparing for batch validation...");
        var combinedCode = CombineGeneratedCode(tasks);
        
        // Step 3: Validate the combined code
        Console.WriteLine("\n🔍 Validating combined code (resolving inter-references)...");
        
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ValidationResult validationResult;
            
            // Use build service if available and output directory is specified
            if (_buildService != null && !string.IsNullOrEmpty(_outputDirectory))
            {
                Console.WriteLine($"   Building project at: {_outputDirectory}");
                var projectName = $"GeneratedCode_{DateTime.Now:yyyyMMdd_HHmmss}";
                // Use the new method that creates separate files and adds package references
                var buildResult = await _buildService.CreateProjectFromTasksAsync(_outputDirectory, projectName, tasks);
                validationResult = _buildService.ConvertToValidationResult(buildResult);
                
                if (buildResult.Success)
                {
                    Console.WriteLine($"   📁 Project created at: {buildResult.ProjectPath}");
                    if (buildResult.GeneratedFiles.Any())
                    {
                        Console.WriteLine($"   📄 Generated {buildResult.GeneratedFiles.Count} code file(s)");
                    }
                }
            }
            else
            {
                // Fallback to Roslyn validation
                validationResult = await _validatorService.ValidateCodeAsync(combinedCode);
            }
            
            if (validationResult.IsValid)
            {
                // Mark all tasks as batch validated and track attempt count
                foreach (var task in tasks)
                {
                    task.ValidationErrors.Clear();
                    task.ValidationAttemptCount = attempt + 1;
                }
                Console.WriteLine("✅ Combined code validated successfully! All inter-references resolved.");
                
                // Generate summaries for all tasks after batch validation
                await GenerateSummariesForAllTasksAsync(tasks, completedTasks);
                
                break;
            }
            else
            {
                Console.WriteLine($"❌ Combined code validation failed (attempt {attempt + 1}/{MaxRetries})");
                Console.WriteLine($"   Errors: {string.Join(", ", validationResult.Errors.Take(5))}");
                
                if (attempt < MaxRetries - 1)
                {
                    // Identify which tasks are causing errors and regenerate only those
                    var tasksToRegenerate = await IdentifyProblematicTasks(tasks, validationResult);
                    
                    if (tasksToRegenerate.Any())
                    {
                        Console.WriteLine($"\n🔄 Regenerating {tasksToRegenerate.Count} task(s) with error context...");
                        await RegenerateSpecificTasksAsync(tasksToRegenerate, validationResult, completedTasks);
                        
                        // Re-validate regenerated tasks individually
                        foreach (var task in tasksToRegenerate)
                        {
                            await ValidateTaskIndividuallyAsync(task);
                        }
                        
                        // Recombine code
                        combinedCode = CombineGeneratedCode(tasks);
                    }
                    else
                    {
                        // If we can't identify specific tasks, regenerate all
                        Console.WriteLine($"\n🔄 Could not identify specific problematic tasks. Regenerating all tasks...");
                        await RegenerateProblematicTasksAsync(tasks, validationResult, completedTasks);
                        combinedCode = CombineGeneratedCode(tasks);
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Combined code validation failed after {MaxRetries} attempts. " +
                        $"Errors: {string.Join(", ", validationResult.Errors)}");
                }
            }
        }

        return tasks;
    }

    /// <summary>
    /// Generates and validates code for a single task (with retry logic).
    /// </summary>
    private async Task<TaskNode> GenerateAndValidateTaskAsync(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        Console.WriteLine($"Generating code for task: {task.Id} - {task.Description}");

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Generate code for the task
                if (string.IsNullOrEmpty(task.GeneratedCode) || attempt > 0)
                {
                    task.GeneratedCode = await _openAIService.GenerateCodeAsync(task, completedTasks);
                    Console.WriteLine($"✓ Generated code for task {task.Id}");
                }

                // Validate the generated code individually
                var validationResult = await _validatorService.ValidateCodeAsync(task.GeneratedCode);
                
                if (validationResult.IsValid)
                {
                    task.IsValidated = true;
                    task.ValidationErrors.Clear();
                    Console.WriteLine($"✓ Task {task.Id} validated successfully");
                    return task;
                }
                else
                {
                    task.IsValidated = false;
                    task.ValidationErrors = validationResult.Errors;
                    
                    // Check if errors are due to missing references (expected in hybrid mode)
                    var hasMissingReferences = validationResult.Errors.Any(e => 
                        e.Contains("could not be found") || 
                        e.Contains("does not exist") ||
                        e.Contains("missing"));
                    
                    if (hasMissingReferences && attempt == 0)
                    {
                        // This is expected - inter-references will be resolved in batch validation
                        Console.WriteLine($"⚠️  Task {task.Id} has potential inter-reference issues (will be resolved in batch validation)");
                        return task;
                    }
                    
                    Console.WriteLine($"❌ Task {task.Id} validation failed (attempt {attempt + 1}): {string.Join(", ", validationResult.Errors.Take(3))}");

                    if (attempt < MaxRetries - 1)
                    {
                        // Re-prompt with errors
                        task.GeneratedCode = await _openAIService.RegenerateCodeWithErrorsAsync(task, validationResult);
                        task.RetryCount++;
                    }
                }
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Console.WriteLine($"HTTP error for task {task.Id} (attempt {attempt + 1}): {ex.Message}");
                task.RetryCount++;
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (Exception ex) when (attempt < MaxRetries - 1)
            {
                Console.WriteLine($"Error for task {task.Id} (attempt {attempt + 1}): {ex.Message}");
                task.RetryCount++;
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        // Even if individual validation failed, return the task
        // Batch validation will determine if it's actually problematic
        return task;
    }

    /// <summary>
    /// Validates a task individually (used after regeneration).
    /// </summary>
    private async Task ValidateTaskIndividuallyAsync(TaskNode task)
    {
        var validationResult = await _validatorService.ValidateCodeAsync(task.GeneratedCode);
        
        if (validationResult.IsValid)
        {
            task.IsValidated = true;
            task.ValidationErrors.Clear();
            Console.WriteLine($"   ✓ Task {task.Id} re-validated successfully");
        }
        else
        {
            task.IsValidated = false;
            task.ValidationErrors = validationResult.Errors;
            Console.WriteLine($"   ⚠️  Task {task.Id} still has validation issues (may be resolved in batch)");
        }
    }
}
