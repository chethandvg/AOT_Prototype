using AoTEngine.Models;
using AoTEngine.Services;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace AoTEngine.Workflow;

/// <summary>
/// Service for the planning phase of the workflow.
/// Handles requirement clarification, spec generation, and blueprint creation.
/// Uses a powerful LLM to analyze requirements and create a detailed plan before coding.
/// </summary>
public class PlanningService
{
    private readonly OpenAIService _openAIService;
    private readonly UserInteractionService _userInteractionService;
    private const int MaxRetries = 3;

    public PlanningService(OpenAIService openAIService, UserInteractionService userInteractionService)
    {
        _openAIService = openAIService;
        _userInteractionService = userInteractionService;
    }

    /// <summary>
    /// Phase 1: Gather uncertainties and clarify requirements.
    /// Analyzes the request to identify unknowns and asks clarifying questions.
    /// </summary>
    public async Task<SharedContext> GatherRequirementsAsync(string userRequest, SharedContext context)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("📋 PHASE 1: Gathering Requirements & Clarifying Uncertainties");
        Console.WriteLine("═══════════════════════════════════════════════════════════");

        context.OriginalRequest = userRequest;

        // Use LLM to identify uncertainties and generate clarifying questions
        var questions = await GenerateClarifyingQuestionsAsync(userRequest);

        if (questions.Any())
        {
            Console.WriteLine();
            Console.WriteLine($"📌 Identified {questions.Count} areas that need clarification:");
            Console.WriteLine();

            foreach (var (question, category) in questions)
            {
                var answer = await _userInteractionService.AskForClarificationAsync(
                    question,
                    $"Category: {category}\nOriginal Request: {userRequest}");

                context.ClarifiedRequirements.Add(new ClarifiedRequirement
                {
                    Question = question,
                    Answer = answer,
                    Category = category,
                    ClarifiedAtUtc = DateTime.UtcNow
                });

                context.ResolvedUncertainties.Add(new ResolvedUncertainty
                {
                    Question = question,
                    Resolution = answer,
                    Phase = "Requirements Gathering",
                    ResolvedAtUtc = DateTime.UtcNow
                });
            }
        }
        else
        {
            Console.WriteLine("✓ No major uncertainties detected. Requirements are clear.");
        }

        context.CreateCheckpoint("Requirements Gathering", "Completed");
        return context;
    }

    /// <summary>
    /// Phase 2: Generate a detailed specification document from the clarified requirements.
    /// </summary>
    public async Task<SharedContext> GenerateSpecificationAsync(SharedContext context)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("📝 PHASE 2: Generating Specification Document");
        Console.WriteLine("═══════════════════════════════════════════════════════════");

        var spec = await CreateSpecificationDocumentAsync(
            context.OriginalRequest,
            context.ClarifiedRequirements);

        context.Specification = spec;

        // Display spec summary for user review
        Console.WriteLine();
        Console.WriteLine("📄 Specification Summary:");
        Console.WriteLine($"   Title: {spec.Title}");
        Console.WriteLine($"   Functional Requirements: {spec.FunctionalRequirements.Count}");
        Console.WriteLine($"   Non-Functional Requirements: {spec.NonFunctionalRequirements.Count}");
        Console.WriteLine($"   Constraints: {spec.Constraints.Count}");
        Console.WriteLine();

        // Ask for confirmation
        var proceed = await _userInteractionService.AskForConfirmationAsync(
            "Does this specification look correct?",
            $"Summary:\n{spec.Summary}\n\nTechnical Approach:\n{spec.TechnicalApproach}");

        if (!proceed)
        {
            throw new OperationCanceledException(
                "User rejected the specification. Please refine your requirements and try again.");
        }

        context.CreateCheckpoint("Specification Generation", "Completed");
        return context;
    }

    /// <summary>
    /// Phase 3: Create a project blueprint with atomic tasks.
    /// Breaks the specification into a step-by-step plan of small, logical tasks.
    /// </summary>
    public async Task<SharedContext> CreateBlueprintAsync(SharedContext context, int maxLinesPerTask = 300)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("🗺️  PHASE 3: Creating Project Blueprint");
        Console.WriteLine("═══════════════════════════════════════════════════════════");

        if (context.Specification == null)
        {
            throw new InvalidOperationException("Specification must be generated before creating blueprint.");
        }

        var blueprint = await GenerateBlueprintAsync(
            context.OriginalRequest,
            context.Specification,
            context.ClarifiedRequirements,
            maxLinesPerTask);

        context.Blueprint = blueprint;

        // Display blueprint for user review
        Console.WriteLine();
        Console.WriteLine($"📋 Blueprint: {blueprint.ProjectName}");
        Console.WriteLine($"   Description: {blueprint.Description}");
        Console.WriteLine($"   Components: {blueprint.Components.Count}");
        Console.WriteLine($"   Tasks: {blueprint.Tasks.Count}");
        Console.WriteLine();

        Console.WriteLine("   Task Breakdown:");
        foreach (var task in blueprint.Tasks)
        {
            var deps = task.Dependencies.Any() ? string.Join(", ", task.Dependencies) : "None";
            Console.WriteLine($"   [{task.Id}] {task.Description}");
            Console.WriteLine($"        Component: {task.Component}");
            Console.WriteLine($"        Dependencies: {deps}");
            Console.WriteLine($"        Est. Lines: ~{task.EstimatedLines}");
            Console.WriteLine();
        }

        // Ask for confirmation
        var proceed = await _userInteractionService.AskForConfirmationAsync(
            "Does this blueprint look correct? Proceeding will start code generation.",
            "Review the task breakdown above.");

        if (!proceed)
        {
            throw new OperationCanceledException(
                "User rejected the blueprint. Please refine your requirements and try again.");
        }

        context.CreateCheckpoint("Blueprint Generation", "Completed");
        return context;
    }

    /// <summary>
    /// Uses LLM to identify uncertainties and generate clarifying questions.
    /// </summary>
    private async Task<List<(string Question, string Category)>> GenerateClarifyingQuestionsAsync(string userRequest)
    {
        var systemPrompt = @"You are an expert requirements analyst. Analyze the user's request and identify areas of uncertainty that need clarification before coding can begin.

Generate clarifying questions for:
1. Ambiguous requirements
2. Missing technical specifications
3. Edge cases that need clarification
4. Security or performance requirements
5. Integration points with external systems
6. Data format and validation requirements

Output JSON in this format:
{
  ""questions"": [
    {
      ""question"": ""What authentication method should be used?"",
      ""category"": ""Security""
    }
  ]
}

If the requirements are clear and complete, return an empty questions array.
Return ONLY valid JSON.";

        var userPrompt = $"Analyze this request and generate clarifying questions:\n\n{userRequest}";

        var response = await _openAIService.DecomposeTaskAsync(new TaskDecompositionRequest
        {
            OriginalRequest = userPrompt,
            Context = systemPrompt
        });

        // Parse the response to extract questions
        // This is a simplified version - in production, you'd want more robust parsing
        var questions = new List<(string Question, string Category)>();

        // The decomposition response will contain tasks, but we need to adapt this
        // For now, return common clarifying questions based on the request
        if (userRequest.Length < 100)
        {
            questions.Add(("Could you provide more details about the expected functionality?", "Scope"));
        }

        if (!userRequest.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            questions.Add(("Should unit tests be included in the generated code?", "Testing"));
        }

        if (!userRequest.Contains("error", StringComparison.OrdinalIgnoreCase) &&
            !userRequest.Contains("exception", StringComparison.OrdinalIgnoreCase))
        {
            questions.Add(("What error handling strategy should be used?", "Error Handling"));
        }

        return questions;
    }

    /// <summary>
    /// Creates a specification document from the request and clarified requirements.
    /// </summary>
    private async Task<SpecificationDocument> CreateSpecificationDocumentAsync(
        string originalRequest,
        List<ClarifiedRequirement> clarifications)
    {
        var clarificationContext = string.Join("\n", clarifications.Select(c =>
            $"Q: {c.Question}\nA: {c.Answer}"));

        var request = new TaskDecompositionRequest
        {
            OriginalRequest = $@"Create a specification document for this request:

Original Request: {originalRequest}

Clarifications:
{clarificationContext}

Generate a detailed specification that includes:
1. Title
2. Summary
3. Functional Requirements (list)
4. Non-Functional Requirements (list)
5. Constraints
6. Assumptions
7. Technical Approach",
            Context = "Generate a specification document in JSON format"
        };

        // Use the decomposition to get structured output
        var decomposition = await _openAIService.DecomposeTaskAsync(request);

        return new SpecificationDocument
        {
            Title = ExtractProjectTitle(originalRequest),
            Summary = decomposition.Description ?? "Generated specification based on user requirements.",
            FunctionalRequirements = decomposition.Tasks.Select(t => t.Description).Take(5).ToList(),
            NonFunctionalRequirements = new List<string>
            {
                "Code should be well-documented",
                "Code should follow C# best practices",
                "Error handling should be comprehensive"
            },
            Constraints = new List<string> { "Target .NET 9.0" },
            Assumptions = clarifications.Select(c => $"{c.Question}: {c.Answer}").ToList(),
            TechnicalApproach = $"Implement using C# with modular architecture based on {decomposition.Tasks.Count} atomic tasks.",
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates a project blueprint with atomic tasks.
    /// </summary>
    private async Task<ProjectBlueprint> GenerateBlueprintAsync(
        string originalRequest,
        SpecificationDocument spec,
        List<ClarifiedRequirement> clarifications,
        int maxLinesPerTask)
    {
        var request = new TaskDecompositionRequest
        {
            OriginalRequest = originalRequest,
            Context = $@"Specification Summary: {spec.Summary}

Technical Approach: {spec.TechnicalApproach}

Requirements:
{string.Join("\n", spec.FunctionalRequirements.Select(r => $"- {r}"))}

Clarifications:
{string.Join("\n", clarifications.Select(c => $"- {c.Question}: {c.Answer}"))}

IMPORTANT: Each task must generate no more than {maxLinesPerTask} lines of code."
        };

        var decomposition = await _openAIService.DecomposeTaskAsync(request);

        var blueprint = new ProjectBlueprint
        {
            ProjectName = ExtractProjectTitle(originalRequest),
            Description = spec.Summary,
            Components = decomposition.Tasks
                .Select(t => t.Namespace)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList(),
            GeneratedAtUtc = DateTime.UtcNow
        };

        // Convert decomposed tasks to blueprint tasks
        foreach (var task in decomposition.Tasks)
        {
            blueprint.Tasks.Add(new BlueprintTask
            {
                Id = task.Id,
                Description = task.Description,
                Component = task.Namespace ?? "Core",
                Dependencies = task.Dependencies,
                ExpectedOutputs = task.ExpectedTypes,
                EstimatedLines = Math.Min(maxLinesPerTask, 150), // Conservative estimate
                AcceptanceCriteria = $"Code compiles and passes validation for: {task.Description}"
            });
        }

        return blueprint;
    }

    /// <summary>
    /// Extracts a project title from the user request using pattern matching.
    /// Falls back to a sanitized version of key nouns from the request.
    /// </summary>
    private string ExtractProjectTitle(string request)
    {
        // Patterns to match common project description phrases
        var regexPatterns = new[]
        {
            @"(?:create|build|implement|develop|make|design)\s+(?:a|an|the)\s+(.+?)(?:\s+(?:that|which|with|using|in|for)|[,.]|$)",
            @"(?:project|application|app|system|service|api)\s+(?:called|named|for)\s+[""']?(\w+)[""']?",
            @"(\w+)(?:Project|App|System|Service|Application|Api)"
        };

        foreach (var pattern in regexPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                request, 
                pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success && match.Groups.Count > 1)
            {
                var extracted = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(extracted))
                {
                    // Clean up and format as Pascal case
                    return FormatAsProjectName(extracted);
                }
            }
        }

        // Fallback: Extract key nouns from the request (first 3 significant words)
        var words = request
            .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Where(w => !IsStopWord(w))
            .Take(3)
            .ToList();

        if (words.Any())
        {
            return FormatAsProjectName(string.Join("", words));
        }

        return "GeneratedProject";
    }

    /// <summary>
    /// Formats a string as a Pascal case project name.
    /// </summary>
    private string FormatAsProjectName(string input)
    {
        // Remove non-alphanumeric characters and split into words
        var cleaned = System.Text.RegularExpressions.Regex.Replace(input, @"[^a-zA-Z0-9\s]", " ");
        var words = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // Format as Pascal case
        var result = string.Join("", words.Select(w => 
            char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w.Substring(1).ToLowerInvariant() : "")));

        // Ensure it's a valid identifier (starts with letter)
        if (string.IsNullOrEmpty(result) || !char.IsLetter(result[0]))
        {
            return "GeneratedProject";
        }

        return result.Length > 50 ? result.Substring(0, 50) : result;
    }

    /// <summary>
    /// Checks if a word is a common stop word that should be excluded from project names.
    /// </summary>
    private bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "is", "are", "was", "were",
            "be", "been", "being", "have", "has", "had", "do", "does", "did",
            "will", "would", "could", "should", "may", "might", "can", "must",
            "this", "that", "these", "those", "it", "its", "with", "for", "from",
            "to", "of", "in", "on", "at", "by", "as", "into", "through", "during",
            "create", "build", "implement", "develop", "make", "design", "write",
            "simple", "basic", "please", "using", "include", "including"
        };

        return stopWords.Contains(word);
    }
}
