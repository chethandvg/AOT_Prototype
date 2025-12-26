using AoTEngine.Models;

namespace AoTEngine.Services;

/// <summary>
/// Service for handling user interactions when uncertainties arise.
/// This is the main partial class containing prompt and clarification methods.
/// </summary>
/// <remarks>
/// This class is split into multiple partial class files for maintainability:
/// - UserInteractionService.cs (this file): Prompt and clarification methods
/// - UserInteractionService.UncertaintyDetection.cs: Uncertainty detection logic
/// </remarks>
public partial class UserInteractionService
{
    /// <summary>
    /// Asks the user for clarification when there's uncertainty.
    /// </summary>
    public Task<string> AskForClarificationAsync(string question, string context = "")
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("⚠️  UNCERTAINTY DETECTED - User Input Required");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        
        if (!string.IsNullOrWhiteSpace(context))
        {
            Console.WriteLine();
            Console.WriteLine("Context:");
            Console.WriteLine(context);
        }
        
        Console.WriteLine();
        Console.WriteLine("Question:");
        Console.WriteLine(question);
        Console.WriteLine();
        Console.Write("Your response: ");
        
        var response = Console.ReadLine();
        
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
        
        return Task.FromResult(response ?? string.Empty);
    }

    /// <summary>
    /// Asks the user to choose from multiple options.
    /// </summary>
    public Task<string> AskForChoiceAsync(string question, List<string> options, string context = "")
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("⚠️  UNCERTAINTY DETECTED - User Input Required");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        
        if (!string.IsNullOrWhiteSpace(context))
        {
            Console.WriteLine();
            Console.WriteLine("Context:");
            Console.WriteLine(context);
        }
        
        Console.WriteLine();
        Console.WriteLine("Question:");
        Console.WriteLine(question);
        Console.WriteLine();
        Console.WriteLine("Options:");
        
        for (int i = 0; i < options.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {options[i]}");
        }
        
        Console.WriteLine();
        Console.Write($"Enter your choice (1-{options.Count}): ");
        
        var choice = Console.ReadLine();
        
        if (int.TryParse(choice, out int index) && index >= 1 && index <= options.Count)
        {
            var selectedOption = options[index - 1];
            Console.WriteLine($"Selected: {selectedOption}");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine();
            return Task.FromResult(selectedOption);
        }
        
        Console.WriteLine("Invalid choice. Using first option as default.");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
        return Task.FromResult(options[0]);
    }

    /// <summary>
    /// Asks the user for confirmation.
    /// </summary>
    public Task<bool> AskForConfirmationAsync(string question, string context = "")
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("⚠️  Confirmation Required");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        
        if (!string.IsNullOrWhiteSpace(context))
        {
            Console.WriteLine();
            Console.WriteLine("Context:");
            Console.WriteLine(context);
        }
        
        Console.WriteLine();
        Console.WriteLine(question);
        Console.Write("Proceed? (y/n): ");
        
        var response = Console.ReadLine();
        var confirmed = response?.Trim().ToLower().StartsWith("y") ?? false;
        
        Console.WriteLine(confirmed ? "✓ Confirmed" : "✗ Cancelled");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
        
        return Task.FromResult(confirmed);
    }

    /// <summary>
    /// Handles uncertainties for multiple tasks in a batch to improve UX.
    /// Collects all uncertainties upfront and prompts the user once for all questions.
    /// </summary>
    public async Task<List<TaskNode>> HandleMultipleTaskUncertaintiesAsync(List<TaskNode> tasks)
    {
        var allQuestions = new List<(TaskNode Task, string Question, string Context)>();
        
        // Phase 1: Collect all uncertainties from all tasks
        foreach (var task in tasks)
        {
            var uncertainties = DetectUncertainties(task.Description);
            var context = $"Task ID: {task.Id}\nDescription: {task.Description}";
            
            foreach (var uncertainty in uncertainties)
            {
                allQuestions.Add((task, uncertainty, context));
            }
        }
        
        if (!allQuestions.Any())
        {
            return tasks;
        }
        
        // Group identical questions together
        var questionGroups = allQuestions
            .GroupBy(q => q.Question)
            .Select(g => new 
            { 
                Question = g.Key, 
                Tasks = g.Select(x => x.Task).ToList(),
                FirstContext = g.First().Context 
            })
            .ToList();
        
        // Phase 2: Display unique questions and collect answers
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine($"⚠️  {questionGroups.Count} UNIQUE UNCERTAINTIES DETECTED - User Input Required");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Found uncertainties in {allQuestions.Select(q => q.Task.Id).Distinct().Count()} task(s).");
        Console.WriteLine($"Total questions reduced from {allQuestions.Count} to {questionGroups.Count} (duplicates removed).");
        Console.WriteLine("Please answer all questions below:");
        Console.WriteLine();
        
        var answerMap = new Dictionary<string, string>();
        
        for (int i = 0; i < questionGroups.Count; i++)
        {
            var group = questionGroups[i];
            var taskIds = string.Join(", ", group.Tasks.Select(t => t.Id));
            
            Console.WriteLine($"┌─ [{i + 1}/{questionGroups.Count}] Applies to {group.Tasks.Count} task(s): {taskIds}");
            Console.WriteLine($"├─────────────────────────────────────────────────────");
            Console.WriteLine($"│ {group.Question}");
            Console.WriteLine($"└─────────────────────────────────────────────────────");
            Console.Write("Your response: ");
            
            var answer = Console.ReadLine() ?? string.Empty;
            answerMap[group.Question] = answer;
            
            Console.WriteLine();
        }
        
        // Phase 3: Apply answers to all relevant tasks
        foreach (var (task, question, _) in allQuestions)
        {
            var answer = answerMap[question];
            task.Context += $"\n\nClarification for '{question}':\n{answer}";
        }
        
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine($"✓ All {questionGroups.Count} uncertainties resolved and applied to {tasks.Count} task(s)");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
        
        return tasks;
    }

    /// <summary>
    /// Detects uncertainties in task descriptions and prompts user for clarification.
    /// </summary>
    public async Task<TaskNode> HandleTaskUncertaintyAsync(TaskNode task)
    {
        var uncertainties = DetectUncertainties(task.Description);
        
        if (uncertainties.Any())
        {
            var context = $"Task ID: {task.Id}\nDescription: {task.Description}\nContext: {task.Context}";
            
            foreach (var uncertainty in uncertainties)
            {
                var clarification = await AskForClarificationAsync(uncertainty, context);
                
                // Append clarification to task context
                task.Context += $"\n\nClarification for '{uncertainty}':\n{clarification}";
            }
        }
        
        return task;
    }

    /// <summary>
    /// Reviews decomposed tasks with the user and allows modifications.
    /// </summary>
    public async Task<List<TaskNode>> ReviewTasksWithUserAsync(List<TaskNode> tasks)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("📋 Task Decomposition Review");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"The request has been decomposed into {tasks.Count} tasks:");
        Console.WriteLine();
        
        foreach (var task in tasks)
        {
            var deps = task.Dependencies.Any() ? string.Join(", ", task.Dependencies) : "None";
            Console.WriteLine($"Task {task.Id}:");
            Console.WriteLine($"  Description: {task.Description}");
            Console.WriteLine($"  Dependencies: {deps}");
            Console.WriteLine();
        }
        
        var proceed = await AskForConfirmationAsync(
            "Does this task decomposition look correct?",
            "Review the tasks above. You can proceed or cancel to modify the request.");
        
        if (!proceed)
        {
            throw new OperationCanceledException("User cancelled the task decomposition. Please refine your request and try again.");
        }
        
        return tasks;
    }
}
