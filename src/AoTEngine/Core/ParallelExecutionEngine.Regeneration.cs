using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Core;

/// <summary>
/// Partial class containing task regeneration methods.
/// </summary>
public partial class ParallelExecutionEngine
{
    /// <summary>
    /// Maximum retry count for regeneration to prevent infinite loops.
    /// </summary>
    private const int MaxRegenerationRetries = 3;

    /// <summary>
    /// Regenerates specific tasks with targeted error feedback, suggestions, and duplicate type detection.
    /// </summary>
    private async Task RegenerateSpecificTasksAsync(
        List<TaskNode> tasksToRegenerate,
        ValidationResult validationResult,
        Dictionary<string, TaskNode> completedTasks)
    {
        foreach (var task in tasksToRegenerate)
        {
            // Check retry limit to prevent infinite loops
            if (task.RetryCount >= MaxRegenerationRetries)
            {
                Console.WriteLine($"   ⚠️  Task {task.Id} has reached max retries ({MaxRegenerationRetries}). Skipping regeneration.");
                continue;
            }

            try
            {
                Console.WriteLine($"   🔄 Regenerating task {task.Id}...");
                
                // Create task-specific validation result with only relevant errors
                var taskSpecificErrors = (task.ValidationErrors != null && task.ValidationErrors.Any())
                    ? task.ValidationErrors
                    : ExtractTaskRelevantErrors(task, validationResult.Errors);
                
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
                
                // Detect duplicate types and get existing code to reuse
                var (duplicateTypes, existingTypeCode, suggestions) = DetectDuplicateTypes(task, completedTasks);
                
                if (duplicateTypes.Any())
                {
                    Console.WriteLine($"      🔍 Detected {duplicateTypes.Count} duplicate type(s): {string.Join(", ", duplicateTypes)}");
                    Console.WriteLine($"      💡 Providing existing code for reuse");
                }
                
                task.GeneratedCode = await _openAIService.RegenerateCodeWithErrorsAsync(
                    task, 
                    modifiedValidationResult,
                    suggestions,
                    existingTypeCode);
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
    /// Detects if a task defines types that already exist in other completed tasks.
    /// Returns the duplicate type names, existing code to reuse, and suggestions.
    /// </summary>
    private (List<string> duplicateTypes, string? existingCode, List<string> suggestions) DetectDuplicateTypes(
        TaskNode task,
        Dictionary<string, TaskNode> completedTasks)
    {
        var duplicateTypes = new List<string>();
        var existingCodeBuilder = new System.Text.StringBuilder();
        var suggestions = new List<string>();
        
        if (string.IsNullOrWhiteSpace(task.GeneratedCode))
            return (duplicateTypes, null, suggestions);
        
        // Extract types defined in this task
        var taskTypes = ExtractDefinedTypes(task.GeneratedCode);
        
        // Check against all completed tasks
        foreach (var completedTask in completedTasks.Values.Where(t => t.Id != task.Id))
        {
            if (string.IsNullOrWhiteSpace(completedTask.GeneratedCode))
                continue;
            
            var existingTypes = ExtractDefinedTypes(completedTask.GeneratedCode);
            
            // Find overlapping type names
            var overlappingTypes = taskTypes.Keys.Intersect(existingTypes.Keys).ToList();
            
            foreach (var typeName in overlappingTypes)
            {
                var taskType = taskTypes[typeName];
                var existingType = existingTypes[typeName];
                
                // Check if they're in the same namespace (true duplicate vs different domain)
                if (IsSameDomainContext(taskType, existingType))
                {
                    duplicateTypes.Add(typeName);
                    
                    // Check for conflicting members (would break existing code)
                    var conflictingMembers = FindConflictingMembers(taskType.code, existingType.code);
                    
                    if (conflictingMembers.Any())
                    {
                        suggestions.Add($"⚠️ Type '{typeName}' exists in task '{completedTask.Id}' with CONFLICTING members: {string.Join(", ", conflictingMembers)}. " +
                                       $"Do NOT modify existing signatures. Only ADD new members if needed.");
                    }
                    else
                    {
                        suggestions.Add($"Type '{typeName}' already exists in task '{completedTask.Id}'. " +
                                       $"REUSE this type - do not recreate it. If you need to extend it, add new members without changing existing ones.");
                    }
                    
                    // Add existing code for reference
                    existingCodeBuilder.AppendLine($"// From task: {completedTask.Id}");
                    existingCodeBuilder.AppendLine(existingType.code);
                    existingCodeBuilder.AppendLine();
                }
                else
                {
                    // Different namespace - just warn about potential ambiguity
                    suggestions.Add($"Note: Type '{typeName}' exists in different namespace in task '{completedTask.Id}'. " +
                                   $"Use fully qualified names if needed to avoid ambiguity.");
                }
            }
        }
        
        var existingCode = existingCodeBuilder.Length > 0 ? existingCodeBuilder.ToString() : null;
        return (duplicateTypes, existingCode, suggestions);
    }

    /// <summary>
    /// Extracts all defined types (class, interface, enum, struct, record) from code.
    /// Returns a dictionary of type name to (namespace, full code).
    /// </summary>
    private Dictionary<string, (string? ns, string code)> ExtractDefinedTypes(string code)
    {
        var types = new Dictionary<string, (string? ns, string code)>();
        
        // Extract namespace
        var nsMatch = System.Text.RegularExpressions.Regex.Match(
            code,
            @"namespace\s+([A-Za-z0-9_.]+)");
        var ns = nsMatch.Success ? nsMatch.Groups[1].Value : null;
        
        // Extract type definitions
        // Note: Uses regex for quick analysis. For production use, consider Roslyn syntax trees for more accurate parsing.
        var typePattern = @"(?:public|internal|private|protected)?\s*(?:abstract|sealed|partial|static)?\s*(?:class|interface|enum|struct|record)\s+([A-Z][a-zA-Z0-9_]+)(?:<[^>]+>)?(?:\s*:\s*[^{]+)?\s*\{";
        var typeMatches = System.Text.RegularExpressions.Regex.Matches(code, typePattern);
        
        foreach (System.Text.RegularExpressions.Match match in typeMatches)
        {
            var typeName = match.Groups[1].Value;
            
            // Extract the full type definition (including body)
            var startIndex = match.Index;
            var typeCode = ExtractTypeBody(code, startIndex);
            
            if (!string.IsNullOrEmpty(typeCode))
            {
                types[typeName] = (ns, typeCode);
            }
        }
        
        return types;
    }

    /// <summary>
    /// Extracts the full type body starting from a given index (handles nested braces).
    /// </summary>
    private string ExtractTypeBody(string code, int startIndex)
    {
        var braceCount = 0;
        var started = false;
        var endIndex = startIndex;
        
        for (int i = startIndex; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                braceCount++;
                started = true;
            }
            else if (code[i] == '}')
            {
                braceCount--;
                if (started && braceCount == 0)
                {
                    endIndex = i + 1;
                    break;
                }
            }
        }
        
        if (endIndex > startIndex && started)
        {
            return code.Substring(startIndex, endIndex - startIndex);
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Checks if two type definitions are in the same domain context (namespace).
    /// </summary>
    private bool IsSameDomainContext((string? ns, string code) type1, (string? ns, string code) type2)
    {
        // If both have namespaces, they must match
        if (!string.IsNullOrEmpty(type1.ns) && !string.IsNullOrEmpty(type2.ns))
        {
            return type1.ns == type2.ns;
        }
        
        // If neither has a namespace, they're in the same (global) context
        if (string.IsNullOrEmpty(type1.ns) && string.IsNullOrEmpty(type2.ns))
        {
            return true;
        }
        
        // One has namespace, one doesn't - different contexts
        return false;
    }

    /// <summary>
    /// Finds conflicting member signatures between two type definitions.
    /// Returns member names that have conflicting signatures.
    /// </summary>
    private List<string> FindConflictingMembers(string code1, string code2)
    {
        var conflicts = new List<string>();
        
        // Extract method signatures from both
        var methods1 = ExtractMethodSignatures(code1);
        var methods2 = ExtractMethodSignatures(code2);
        
        // Extract property signatures
        var props1 = ExtractPropertySignatures(code1);
        var props2 = ExtractPropertySignatures(code2);
        
        // Find methods with same name but different signatures
        foreach (var method1 in methods1)
        {
            foreach (var method2 in methods2.Where(m => m.name == method1.name))
            {
                if (method1.signature != method2.signature)
                {
                    conflicts.Add($"{method1.name}()");
                }
            }
        }
        
        // Find properties with same name but different types
        foreach (var prop1 in props1)
        {
            foreach (var prop2 in props2.Where(p => p.name == prop1.name))
            {
                if (prop1.type != prop2.type)
                {
                    conflicts.Add(prop1.name);
                }
            }
        }
        
        return conflicts.Distinct().ToList();
    }

    /// <summary>
    /// Extracts method signatures from code.
    /// Note: Uses regex for quick analysis. For production use, consider Roslyn syntax trees.
    /// </summary>
    private List<(string name, string signature)> ExtractMethodSignatures(string code)
    {
        var methods = new List<(string name, string signature)>();
        
        // Simplified pattern - matches most common method declarations
        var pattern = @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:virtual\s+)?(?:override\s+)?(?:abstract\s+)?(\S+)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(([^)]*)\)";
        var matches = System.Text.RegularExpressions.Regex.Matches(code, pattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var returnType = match.Groups[1].Value;
            var name = match.Groups[2].Value;
            var parameters = match.Groups[3].Value.Trim();
            
            // Skip constructors - constructor names match the return type (which would be the class name)
            // This is the primary check for constructors
            if (returnType != name)
            {
                methods.Add((name, $"{returnType} {name}({parameters})"));
            }
        }
        
        return methods;
    }

    /// <summary>
    /// Extracts property signatures from code.
    /// Note: Uses regex for quick analysis. For production use, consider Roslyn syntax trees.
    /// </summary>
    private List<(string name, string type)> ExtractPropertySignatures(string code)
    {
        var props = new List<(string name, string type)>();
        
        var pattern = @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:virtual\s+)?(?:override\s+)?(\S+)\s+([A-Z][A-Za-z0-9_]*)\s*\{\s*get";
        var matches = System.Text.RegularExpressions.Regex.Matches(code, pattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            props.Add((match.Groups[2].Value, match.Groups[1].Value));
        }
        
        return props;
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
        var matchingErrors = allErrors.Where(error => 
            definedTypes.Any(type => error.Contains(type)));
        relevantErrors.AddRange(matchingErrors);
        
        // Also check for namespace issues
        var namespaceMatch = System.Text.RegularExpressions.Regex.Match(
            task.GeneratedCode,
            @"namespace\s+([A-Za-z0-9_.]+)");
        
        if (namespaceMatch.Success)
        {
            var taskNamespace = namespaceMatch.Groups[1].Value;
            var namespaceErrors = allErrors.Where(error => 
                error.Contains(taskNamespace) && !relevantErrors.Contains(error));
            relevantErrors.AddRange(namespaceErrors);
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
        
        foreach (var task in tasks.Where(t => !string.IsNullOrWhiteSpace(t.GeneratedCode)))
        {
            // Check retry limit to prevent infinite loops
            if (task.RetryCount >= MaxRegenerationRetries)
            {
                Console.WriteLine($"   ⚠️  Task {task.Id} has reached max retries ({MaxRegenerationRetries}). Skipping regeneration.");
                continue;
            }

            try
            {
                var modifiedValidationResult = new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string>(validationResult.Errors)
                };
                
                // Detect duplicate types and get suggestions
                var (duplicateTypes, existingTypeCode, suggestions) = DetectDuplicateTypes(task, completedTasks);
                
                if (duplicateTypes.Any())
                {
                    Console.WriteLine($"   🔍 Task {task.Id}: Found {duplicateTypes.Count} duplicate type(s)");
                }
                
                task.GeneratedCode = await _openAIService.RegenerateCodeWithErrorsAsync(
                    task, 
                    modifiedValidationResult,
                    suggestions,
                    existingTypeCode);
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
