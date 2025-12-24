using AoTEngine.Models;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Service for merging code snippets and validating contracts.
/// </summary>
public class CodeMergerService
{
    private readonly CodeValidatorService _validatorService;

    public CodeMergerService(CodeValidatorService validatorService)
    {
        _validatorService = validatorService;
    }

    /// <summary>
    /// Merges all generated code snippets into a final solution.
    /// </summary>
    public async Task<string> MergeCodeSnippetsAsync(List<TaskNode> tasks)
    {
        var mergedCode = new StringBuilder();
        var usings = new HashSet<string>();
        var namespaces = new Dictionary<string, StringBuilder>();

        // Extract using statements and organize by namespace
        foreach (var task in tasks.OrderBy(t => t.Id))
        {
            if (string.IsNullOrWhiteSpace(task.GeneratedCode))
                continue;

            var lines = task.GeneratedCode.Split('\n');
            var currentNamespace = "Global";
            var namespaceContent = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Collect using statements
                if (trimmedLine.StartsWith("using ") && trimmedLine.EndsWith(";"))
                {
                    usings.Add(trimmedLine);
                }
                // Track namespace
                else if (trimmedLine.StartsWith("namespace "))
                {
                    currentNamespace = trimmedLine.Replace("namespace ", "").Replace(";", "").Trim();
                    if (!namespaces.ContainsKey(currentNamespace))
                    {
                        namespaces[currentNamespace] = new StringBuilder();
                    }
                }
                // Add content to current namespace
                else if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    if (!namespaces.ContainsKey(currentNamespace))
                    {
                        namespaces[currentNamespace] = new StringBuilder();
                    }
                    namespaces[currentNamespace].AppendLine(line);
                }
            }
        }

        // Build the merged code
        // Add all using statements
        foreach (var usingStatement in usings.OrderBy(u => u))
        {
            mergedCode.AppendLine(usingStatement);
        }

        mergedCode.AppendLine();

        // Add each namespace with its content
        foreach (var ns in namespaces.OrderBy(kvp => kvp.Key))
        {
            if (ns.Key == "Global")
            {
                mergedCode.AppendLine(ns.Value.ToString());
            }
            else
            {
                mergedCode.AppendLine($"namespace {ns.Key}");
                mergedCode.AppendLine("{");
                mergedCode.AppendLine(ns.Value.ToString());
                mergedCode.AppendLine("}");
            }
        }

        var finalCode = mergedCode.ToString();

        // Validate the merged code
        var validationResult = await _validatorService.ValidateCodeAsync(finalCode);
        if (!validationResult.IsValid)
        {
            Console.WriteLine("Warning: Merged code has validation issues:");
            foreach (var error in validationResult.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }

        return finalCode;
    }

    /// <summary>
    /// Validates that contracts between code snippets are satisfied.
    /// </summary>
    public ValidationResult ValidateContracts(List<TaskNode> tasks)
    {
        var result = new ValidationResult { IsValid = true };

        // Check that all dependencies are satisfied
        var taskIds = tasks.Select(t => t.Id).ToHashSet();
        foreach (var task in tasks)
        {
            foreach (var depId in task.Dependencies)
            {
                if (!taskIds.Contains(depId))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Task {task.Id} depends on missing task {depId}");
                }
            }
        }

        // Check that all tasks were validated
        var unvalidatedTasks = tasks.Where(t => !t.IsValidated).ToList();
        if (unvalidatedTasks.Any())
        {
            result.Warnings.Add($"Tasks not validated: {string.Join(", ", unvalidatedTasks.Select(t => t.Id))}");
        }

        // Check for validation errors in any task
        var tasksWithErrors = tasks.Where(t => t.ValidationErrors.Any()).ToList();
        if (tasksWithErrors.Any())
        {
            result.IsValid = false;
            foreach (var task in tasksWithErrors)
            {
                result.Errors.Add($"Task {task.Id} has validation errors: {string.Join(", ", task.ValidationErrors)}");
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a summary report of the execution.
    /// </summary>
    public string CreateExecutionReport(List<TaskNode> tasks, string mergedCode)
    {
        var report = new StringBuilder();
        report.AppendLine("=== AoT Engine Execution Report ===");
        report.AppendLine();
        report.AppendLine($"Total Tasks: {tasks.Count}");
        report.AppendLine($"Completed Tasks: {tasks.Count(t => t.IsCompleted)}");
        report.AppendLine($"Validated Tasks: {tasks.Count(t => t.IsValidated)}");
        report.AppendLine();

        report.AppendLine("Task Details:");
        foreach (var task in tasks.OrderBy(t => t.Id))
        {
            report.AppendLine($"  - {task.Id}: {task.Description}");
            report.AppendLine($"    Dependencies: {(task.Dependencies.Any() ? string.Join(", ", task.Dependencies) : "None")}");
            report.AppendLine($"    Status: {(task.IsCompleted ? "✓" : "✗")} Completed, {(task.IsValidated ? "✓" : "✗")} Validated");
            if (task.RetryCount > 0)
            {
                report.AppendLine($"    Retry Count: {task.RetryCount}");
            }
        }

        report.AppendLine();
        report.AppendLine($"Merged Code Length: {mergedCode.Length} characters");
        report.AppendLine($"Merged Code Lines: {mergedCode.Split('\n').Length}");

        return report.ToString();
    }
}
