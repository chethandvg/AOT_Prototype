using AoTEngine.Models;
using AoTEngine.Services;
using Newtonsoft.Json;

namespace AoTEngine.Core;

/// <summary>
/// Engine for executing tasks in parallel based on their dependencies.
/// </summary>
public class ParallelExecutionEngine
{
    private readonly OpenAIService _openAIService;
    private readonly CodeValidatorService _validatorService;
    private readonly UserInteractionService _userInteractionService;
    private readonly ProjectBuildService? _buildService;
    private readonly string? _outputDirectory;
    private const int MaxRetries = 3;

    public ParallelExecutionEngine(
        OpenAIService openAIService, 
        CodeValidatorService validatorService,
        UserInteractionService userInteractionService,
        ProjectBuildService? buildService = null,
        string? outputDirectory = null)
    {
        _openAIService = openAIService;
        _validatorService = validatorService;
        _userInteractionService = userInteractionService;
        _buildService = buildService;
        _outputDirectory = outputDirectory;
    }

    /// <summary>
    /// Executes all tasks in the DAG, running independent tasks in parallel.
    /// This version generates all code first, then validates the combined code to resolve inter-references.
    /// Uses ProjectBuildService to build and validate at the specified output directory if available.
    /// </summary>
    public async Task<List<TaskNode>> ExecuteTasksWithBatchValidationAsync(List<TaskNode> tasks)
    {
        // Handle all uncertainties upfront before execution
        await _userInteractionService.HandleMultipleTaskUncertaintiesAsync(tasks);
        
        Console.WriteLine("\n?? Generating code for all tasks...");
        
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
                    Console.WriteLine("\n??  Deadlock detected. Analyzing dependencies...");
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
                completedTasks[task.Id] = task;
            }

            Console.WriteLine($"Generated code for {completedTasks.Count}/{tasks.Count} tasks");
        }

        // Step 2: Combine all generated code
        Console.WriteLine("\n?? Combining all generated code for batch validation...");
        var combinedCode = CombineGeneratedCode(tasks);
        
        // Step 3: Validate the combined code
        Console.WriteLine("\n?? Validating combined code (inter-references will be resolved)...");
        
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ValidationResult validationResult;
            
            // Use build service if available and output directory is specified
            if (_buildService != null && !string.IsNullOrEmpty(_outputDirectory))
            {
                Console.WriteLine($"   Building project at: {_outputDirectory}");
                var projectName = $"GeneratedCode_{DateTime.Now:yyyyMMdd_HHmmss}";
                var buildResult = await _buildService.CreateAndValidateProjectAsync(_outputDirectory, projectName, combinedCode);
                validationResult = _buildService.ConvertToValidationResult(buildResult);
                
                if (buildResult.Success)
                {
                    Console.WriteLine($"   ?? Project created at: {buildResult.ProjectPath}");
                }
            }
            else
            {
                // Fallback to Roslyn validation
                validationResult = await _validatorService.ValidateCodeAsync(combinedCode);
            }
            
            if (validationResult.IsValid)
            {
                // Mark all tasks as validated
                foreach (var task in tasks)
                {
                    task.IsValidated = true;
                    task.ValidationErrors.Clear();
                }
                Console.WriteLine("? Combined code validated successfully! All inter-references resolved.");
                break;
            }
            else
            {
                Console.WriteLine($"? Combined code validation failed (attempt {attempt + 1}/{MaxRetries})");
                Console.WriteLine($"   Errors: {string.Join(", ", validationResult.Errors.Take(3))}");
                
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
    /// Executes all tasks in the DAG, running independent tasks in parallel.
    /// This version uses hybrid validation: validates each task individually first, then validates combined code.
    /// Uses ProjectBuildService to build and validate at the specified output directory if available.
    /// </summary>
    public async Task<List<TaskNode>> ExecuteTasksWithHybridValidationAsync(List<TaskNode> tasks)
    {
        // Handle all uncertainties upfront before execution
        await _userInteractionService.HandleMultipleTaskUncertaintiesAsync(tasks);
        
        Console.WriteLine("\n?? Generating and validating code for all tasks...");
        
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
                    Console.WriteLine("\n??  Deadlock detected. Analyzing dependencies...");
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

        // Step 2: Combine all generated code
        Console.WriteLine("\n?? Combining all generated code for batch validation...");
        var combinedCode = CombineGeneratedCode(tasks);
        
        // Step 3: Validate the combined code
        Console.WriteLine("\n?? Validating combined code (resolving inter-references)...");
        
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ValidationResult validationResult;
            
            // Use build service if available and output directory is specified
            if (_buildService != null && !string.IsNullOrEmpty(_outputDirectory))
            {
                Console.WriteLine($"   Building project at: {_outputDirectory}");
                var projectName = $"GeneratedCode_{DateTime.Now:yyyyMMdd_HHmmss}";
                var buildResult = await _buildService.CreateAndValidateProjectAsync(_outputDirectory, projectName, combinedCode);
                validationResult = _buildService.ConvertToValidationResult(buildResult);
                
                if (buildResult.Success)
                {
                    Console.WriteLine($"   ?? Project created at: {buildResult.ProjectPath}");
                }
            }
            else
            {
                // Fallback to Roslyn validation
                validationResult = await _validatorService.ValidateCodeAsync(combinedCode);
            }
            
            if (validationResult.IsValid)
            {
                // Mark all tasks as batch validated
                foreach (var task in tasks)
                {
                    task.ValidationErrors.Clear();
                }
                Console.WriteLine("? Combined code validated successfully! All inter-references resolved.");
                break;
            }
            else
            {
                Console.WriteLine($"? Combined code validation failed (attempt {attempt + 1}/{MaxRetries})");
                Console.WriteLine($"   Errors: {string.Join(", ", validationResult.Errors.Take(5))}");
                
                if (attempt < MaxRetries - 1)
                {
                    // Identify which tasks are causing errors and regenerate only those
                    var tasksToRegenerate = await IdentifyProblematicTasks(tasks, validationResult);
                    
                    if (tasksToRegenerate.Any())
                    {
                        Console.WriteLine($"\n?? Regenerating {tasksToRegenerate.Count} task(s) with error context...");
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
                        Console.WriteLine($"\n?? Could not identify specific problematic tasks. Regenerating all tasks...");
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
        }

        return tasks;
    }

    /// <summary>
    /// Checks if all dependencies for a task have generated code (for batch validation mode).
    /// </summary>
    private bool AreAllDependenciesGenerated(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        return task.Dependencies.All(depId => completedTasks.ContainsKey(depId));
    }

    /// <summary>
    /// Checks if all dependencies for a task are met (for individual validation mode).
    /// </summary>
    private bool AreAllDependenciesMet(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        return task.Dependencies.All(depId => 
            completedTasks.TryGetValue(depId, out var completedTask) && completedTask.IsValidated);
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
                    Console.WriteLine($"Task {task.Id} validated successfully");
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
                Console.WriteLine($"? Generated code for task {task.Id}");
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

    /// <summary>
    /// Combines all generated code from tasks into a single code snippet.
    /// </summary>
    private string CombineGeneratedCode(List<TaskNode> tasks)
    {
        var usings = new HashSet<string>();
        var namespaces = new Dictionary<string, List<string>>();
        
        foreach (var task in tasks.Where(t => !string.IsNullOrWhiteSpace(t.GeneratedCode)))
        {
            var lines = task.GeneratedCode.Split('\n');
            var currentNamespace = "Global";
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Collect using statements
                if (trimmedLine.StartsWith("using ") && trimmedLine.EndsWith(";"))
                {
                    usings.Add(trimmedLine);
                    continue;
                }
                
                // Track namespace
                if (trimmedLine.StartsWith("namespace "))
                {
                    currentNamespace = trimmedLine.Replace("namespace ", "")
                        .Replace(";", "")
                        .Replace("{", "")
                        .Trim();
                    
                    if (!namespaces.ContainsKey(currentNamespace))
                    {
                        namespaces[currentNamespace] = new List<string>();
                    }
                    continue;
                }
                
                // Skip closing braces for namespaces
                if (trimmedLine == "}" && currentNamespace != "Global")
                {
                    continue;
                }
                
                // Add content to current namespace
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    if (!namespaces.ContainsKey(currentNamespace))
                    {
                        namespaces[currentNamespace] = new List<string>();
                    }
                    namespaces[currentNamespace].Add(line);
                }
            }
        }
        
        // Build combined code
        var combined = new System.Text.StringBuilder();
        
        // Add all using statements
        foreach (var usingStatement in usings.OrderBy(u => u))
        {
            combined.AppendLine(usingStatement);
        }
        
        combined.AppendLine();
        
        // Add each namespace with its content
        foreach (var ns in namespaces.OrderBy(kvp => kvp.Key))
        {
            if (ns.Key == "Global")
            {
                foreach (var line in ns.Value)
                {
                    combined.AppendLine(line);
                }
            }
            else
            {
                combined.AppendLine($"namespace {ns.Key}");
                combined.AppendLine("{");
                foreach (var line in ns.Value)
                {
                    combined.AppendLine(line);
                }
                combined.AppendLine("}");
            }
        }
        
        return combined.ToString();
    }

    /// <summary>
    /// Attempts to regenerate tasks that are causing validation errors.
    /// </summary>
    private async Task RegenerateProblematicTasksAsync(
        List<TaskNode> tasks, 
        ValidationResult validationResult, 
        Dictionary<string, TaskNode> completedTasks)
    {
        Console.WriteLine("\n?? Attempting to fix validation errors by regenerating problematic code...");
        
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
                Console.WriteLine($"   ? Regenerated code for task {task.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ??  Failed to regenerate task {task.Id}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Builds a topologically sorted list of tasks for visualization.
    /// </summary>
    public List<TaskNode> TopologicalSort(List<TaskNode> tasks)
    {
        var sorted = new List<TaskNode>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        var taskDict = tasks.ToDictionary(t => t.Id, t => t);

        void Visit(TaskNode task)
        {
            if (visited.Contains(task.Id))
                return;

            if (visiting.Contains(task.Id))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected involving task {task.Id}");
            }

            visiting.Add(task.Id);

            foreach (var depId in task.Dependencies.Where(depId => taskDict.ContainsKey(depId)))
            {
                Visit(taskDict[depId]);
            }

            visiting.Remove(task.Id);
            visited.Add(task.Id);
            sorted.Add(task);
        }

        foreach (var task in tasks)
        {
            Visit(task);
        }

        return sorted;
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
                    Console.WriteLine($"? Generated code for task {task.Id}");
                }

                // Validate the generated code individually
                var validationResult = await _validatorService.ValidateCodeAsync(task.GeneratedCode);
                
                if (validationResult.IsValid)
                {
                    task.IsValidated = true;
                    task.ValidationErrors.Clear();
                    Console.WriteLine($"? Task {task.Id} validated successfully");
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
                        Console.WriteLine($"??  Task {task.Id} has potential inter-reference issues (will be resolved in batch validation)");
                        return task;
                    }
                    
                    Console.WriteLine($"? Task {task.Id} validation failed (attempt {attempt + 1}): {string.Join(", ", validationResult.Errors.Take(3))}");

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
            Console.WriteLine($"   ? Task {task.Id} re-validated successfully");
        }
        else
        {
            task.IsValidated = false;
            task.ValidationErrors = validationResult.Errors;
            Console.WriteLine($"   ??  Task {task.Id} still has validation issues (may be resolved in batch)");
        }
    }

    /// <summary>
    /// Identifies which tasks are causing validation errors in the combined code.
    /// Uses multiple strategies including error pattern matching, line number analysis, and incremental validation.
    /// </summary>
    private async Task<List<TaskNode>> IdentifyProblematicTasks(List<TaskNode> tasks, ValidationResult validationResult)
    {
        var problematicTasks = new HashSet<TaskNode>();
        
        Console.WriteLine("   ?? Analyzing errors to identify problematic tasks...");
        
        // Strategy 1: Parse error messages to extract file/line information and specific identifiers
        var errorPatterns = new Dictionary<TaskNode, List<string>>();
        
        foreach (var error in validationResult.Errors)
        {
            // Extract class/interface/enum names from error messages
            var typeMatches = System.Text.RegularExpressions.Regex.Matches(
                error, 
                @"(?:type|class|interface|enum|struct|record)\s+['\""]?([A-Z][a-zA-Z0-9_]+)['\""]?|['\""]([A-Z][a-zA-Z0-9_]+)['\""](?:\s+could not be found|\s+does not exist)");
            
            // Extract method names from error messages
            var methodMatches = System.Text.RegularExpressions.Regex.Matches(
                error,
                @"method\s+['\""]?([A-Za-z][a-zA-Z0-9_]+)['\""]?|['\""]([A-Za-z][a-zA-Z0-9_]+)['\""].*does not contain");
            
            // Extract property/field names
            var memberMatches = System.Text.RegularExpressions.Regex.Matches(
                error,
                @"(?:property|field)\s+['\""]?([A-Za-z][a-zA-Z0-9_]+)['\""]?");
            
            var identifiers = new HashSet<string>();
            foreach (System.Text.RegularExpressions.Match match in typeMatches)
            {
                identifiers.Add(match.Groups[1].Value != "" ? match.Groups[1].Value : match.Groups[2].Value);
            }
            foreach (System.Text.RegularExpressions.Match match in methodMatches)
            {
                identifiers.Add(match.Groups[1].Value != "" ? match.Groups[1].Value : match.Groups[2].Value);
            }
            foreach (System.Text.RegularExpressions.Match match in memberMatches)
            {
                identifiers.Add(match.Groups[1].Value);
            }
            
            // Match identifiers to tasks
            foreach (var identifier in identifiers.Where(i => !string.IsNullOrWhiteSpace(i)))
            {
                foreach (var task in tasks)
                {
                    if (string.IsNullOrWhiteSpace(task.GeneratedCode))
                        continue;
                    
                    // Check if this task defines or uses the problematic identifier
                    if (System.Text.RegularExpressions.Regex.IsMatch(
                        task.GeneratedCode,
                        $@"\b(?:class|interface|enum|struct|record|public|private|internal|protected)\s+{identifier}\b"))
                    {
                        if (!errorPatterns.ContainsKey(task))
                            errorPatterns[task] = new List<string>();
                        
                        if (!errorPatterns[task].Contains(error))
                            errorPatterns[task].Add(error);
                        
                        problematicTasks.Add(task);
                    }
                }
            }
        }
        
        // Strategy 2: Incremental validation - add tasks one by one to find culprits
        if (problematicTasks.Count == 0 || problematicTasks.Count == tasks.Count)
        {
            Console.WriteLine("   ?? Running incremental validation to isolate problematic tasks...");
            problematicTasks = await IncrementalValidationAnalysis(tasks);
        }
        
        // Strategy 3: Check for tasks with previous validation errors
        if (problematicTasks.Count == 0)
        {
            foreach (var task in tasks)
            {
                if (task.ValidationErrors != null && task.ValidationErrors.Any(e => 
                    !e.Contains("could not be found") && 
                    !e.Contains("does not exist") &&
                    !e.Contains("namespace")))
                {
                    problematicTasks.Add(task);
                    if (!errorPatterns.ContainsKey(task))
                        errorPatterns[task] = new List<string>();
                    errorPatterns[task].AddRange(task.ValidationErrors);
                }
            }
        }
        
        // Store task-specific errors for targeted regeneration
        foreach (var task in problematicTasks)
        {
            if (errorPatterns.ContainsKey(task))
            {
                task.ValidationErrors = errorPatterns[task];
                Console.WriteLine($"   ?? Identified task {task.Id} with {errorPatterns[task].Count} related error(s)");
            }
            else
            {
                Console.WriteLine($"   ?? Identified task {task.Id} as potentially problematic");
            }
        }
        
        return problematicTasks.ToList();
    }

    /// <summary>
    /// Performs incremental validation by adding tasks one by one to identify which ones cause errors.
    /// </summary>
    private async Task<HashSet<TaskNode>> IncrementalValidationAnalysis(List<TaskNode> tasks)
    {
        var problematicTasks = new HashSet<TaskNode>();
        var validatedTasks = new List<TaskNode>();
        
        // Sort tasks by dependency order
        var sortedTasks = TopologicalSort(tasks);
        
        foreach (var task in sortedTasks)
        {
            if (string.IsNullOrWhiteSpace(task.GeneratedCode))
                continue;
            
            // Create a test combination with all validated tasks + current task
            var testTasks = new List<TaskNode>(validatedTasks) { task };
            var combinedCode = CombineGeneratedCode(testTasks);
            
            var validationResult = await _validatorService.ValidateCodeAsync(combinedCode);
            
            if (validationResult.IsValid)
            {
                // This task is good, add it to validated set
                validatedTasks.Add(task);
                Console.WriteLine($"      ? Task {task.Id} is valid in isolation");
            }
            else
            {
                // This task introduces errors
                problematicTasks.Add(task);
                task.ValidationErrors = validationResult.Errors
                    .Where(e => !validatedTasks.Any(vt => vt.GeneratedCode.Contains(e.Split(' ').FirstOrDefault() ?? "")))
                    .ToList();
                
                Console.WriteLine($"      ??  Task {task.Id} introduces {task.ValidationErrors.Count} error(s)");
                
                // Still add it to validated tasks to continue testing (we'll regenerate it later)
                validatedTasks.Add(task);
            }
        }
        
        return problematicTasks;
    }

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
                Console.WriteLine($"   ?? Regenerating task {task.Id}...");
                
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
                Console.WriteLine($"   ? Regenerated code for task {task.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ??  Failed to regenerate task {task.Id}: {ex.Message}");
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
}
