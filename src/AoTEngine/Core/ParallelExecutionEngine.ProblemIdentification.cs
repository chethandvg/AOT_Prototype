using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Core;

/// <summary>
/// Partial class containing problem identification and task regeneration methods.
/// </summary>
public partial class ParallelExecutionEngine
{
    /// <summary>
    /// Identifies which tasks are causing validation errors in the combined code.
    /// Uses multiple strategies including error pattern matching, line number analysis, and incremental validation.
    /// </summary>
    private async Task<List<TaskNode>> IdentifyProblematicTasks(List<TaskNode> tasks, ValidationResult validationResult)
    {
        var problematicTasks = new HashSet<TaskNode>();
        
        Console.WriteLine("   🔍 Analyzing errors to identify problematic tasks...");
        
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
            Console.WriteLine("   🔄 Running incremental validation to isolate problematic tasks...");
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
                Console.WriteLine($"   🔍 Identified task {task.Id} with {errorPatterns[task].Count} related error(s)");
            }
            else
            {
                Console.WriteLine($"   🔍 Identified task {task.Id} as potentially problematic");
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
                Console.WriteLine($"      ✓ Task {task.Id} is valid in isolation");
            }
            else
            {
                // This task introduces errors
                problematicTasks.Add(task);
                task.ValidationErrors = validationResult.Errors
                    .Where(e => !validatedTasks.Any(vt => vt.GeneratedCode.Contains(e.Split(' ').FirstOrDefault() ?? "")))
                    .ToList();
                
                Console.WriteLine($"      ⚠️  Task {task.Id} introduces {task.ValidationErrors.Count} error(s)");
                
                // Still add it to validated tasks to continue testing (we'll regenerate it later)
                validatedTasks.Add(task);
            }
        }
        
        return problematicTasks;
    }
}
