using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Core;

/// <summary>
/// Partial class containing batch validation execution methods.
/// </summary>
public partial class ParallelExecutionEngine
{
    /// <summary>
    /// Executes all tasks in the DAG, running independent tasks in parallel.
    /// This version generates all code first, then validates the combined code to resolve inter-references.
    /// Uses ProjectBuildService to build and validate at the specified output directory if available.
    /// </summary>
    public async Task<List<TaskNode>> ExecuteTasksWithBatchValidationAsync(List<TaskNode> tasks)
    {
        // Handle all uncertainties upfront before execution
        await _userInteractionService.HandleMultipleTaskUncertaintiesAsync(tasks);
        
        Console.WriteLine("\n🔧 Generating code for all tasks...");
        
        // Step 1: Generate code for all tasks (respecting dependencies)
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
                    // Debug output to help identify the issue
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

            // Generate code for ready tasks in parallel
            var generationTasks = readyTasks.Select(task => GenerateCodeForTaskAsync(task, completedTasks));
            var results = await Task.WhenAll(generationTasks);

            // Mark completed tasks
            foreach (var task in results)
            {
                task.IsCompleted = true;
                task.CompletedAtUtc = DateTime.UtcNow;
                completedTasks[task.Id] = task;
            }

            Console.WriteLine($"Generated code for {completedTasks.Count}/{tasks.Count} tasks");
            
            // Save checkpoint after each batch of code generation
            await SaveCheckpointAsync(tasks, completedTasks);
        }

        // Step 2: Generate draft documentation BEFORE validation
        // This ensures documentation is available even if validation fails
        Console.WriteLine("\n📝 Generating draft documentation for all tasks...");
        await GenerateDraftSummariesForAllTasksAsync(tasks, completedTasks);
        
        // Save checkpoint with draft documentation
        await SaveCheckpointAsync(tasks, completedTasks, "documentation_generated");

        // Step 3: Combine all generated code (still needed for Roslyn validation fallback)
        Console.WriteLine("\n📋 Preparing for batch validation...");
        var combinedCode = CombineGeneratedCode(tasks);
        
        // Step 4: Validate the combined code
        Console.WriteLine("\n🔍 Validating combined code (inter-references will be resolved)...");
        
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
                // Mark all tasks as validated and track attempt count
                foreach (var task in tasks)
                {
                    task.IsValidated = true;
                    task.ValidationErrors.Clear();
                    task.ValidationAttemptCount = attempt + 1;
                    // Update documentation status from draft to final
                    task.DocumentationStatus = "final";
                }
                Console.WriteLine("✅ Combined code validated successfully! All inter-references resolved.");
                Console.WriteLine("✅ Documentation status updated to 'final'.");
                
                // Save final checkpoint
                await SaveCheckpointAsync(tasks, completedTasks, "completed");
                
                break;
            }
            else
            {
                Console.WriteLine($"❌ Combined code validation failed (attempt {attempt + 1}/{MaxRetries})");
                Console.WriteLine($"   Errors: {string.Join(", ", validationResult.Errors.Take(3))}");
                
                // Store validation errors in tasks for checkpoint and potential resume
                StoreValidationErrorsInTasks(tasks, validationResult);
                
                // Save checkpoint with validation errors (for resume capability)
                await SaveCheckpointAsync(tasks, completedTasks, "validation_failed");
                
                if (attempt < MaxRetries - 1)
                {
                    // Try to fix the issues by regenerating problematic tasks
                    await RegenerateProblematicTasksAsync(tasks, validationResult, completedTasks);
                    combinedCode = CombineGeneratedCode(tasks);
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
    /// Generates draft summaries for all tasks before validation.
    /// Marks documentation status as "draft" until validation succeeds.
    /// </summary>
    private async Task GenerateDraftSummariesForAllTasksAsync(List<TaskNode> tasks, Dictionary<string, TaskNode> completedTasks)
    {
        if (_documentationService == null)
        {
            return;
        }
        
        foreach (var task in tasks)
        {
            await GenerateTaskSummaryAsync(task, completedTasks);
            task.DocumentationStatus = "draft";
        }
        
        Console.WriteLine("✅ Draft documentation generated for all tasks.");
    }

    /// <summary>
    /// Stores validation errors in tasks for checkpoint persistence and resume capability.
    /// This allows the system to provide error context when regenerating code after a restart.
    /// </summary>
    private void StoreValidationErrorsInTasks(List<TaskNode> tasks, ValidationResult validationResult)
    {
        // Try to attribute errors to specific tasks
        foreach (var task in tasks.Where(t => !string.IsNullOrWhiteSpace(t.GeneratedCode)))
        {
            var taskErrors = ExtractTaskRelevantErrors(task, validationResult.Errors);
            if (taskErrors.Any())
            {
                task.ValidationErrors = taskErrors;
            }
            else if (!task.ValidationErrors.Any())
            {
                // If no specific errors found, store all errors for context
                task.ValidationErrors = new List<string>(validationResult.Errors);
            }
        }
    }
}
