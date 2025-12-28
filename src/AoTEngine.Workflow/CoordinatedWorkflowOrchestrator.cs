using AoTEngine.Core;
using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Workflow;

/// <summary>
/// Configuration for the coordinated workflow.
/// </summary>
public class WorkflowConfig
{
    public bool EnablePlanningPhase { get; set; } = true;
    public bool EnableClarificationPhase { get; set; } = true;
    public bool EnableBlueprintGeneration { get; set; } = true;
    public bool EnableIterativeVerification { get; set; } = true;
    public int MaxLinesPerTask { get; set; } = 300;
    public bool EnableContractFirst { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Result of the coordinated workflow execution.
/// </summary>
public class WorkflowResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public SharedContext Context { get; set; } = new();
    public string FinalCode { get; set; } = string.Empty;
    public string ExecutionReport { get; set; } = string.Empty;
    public List<TaskNode> Tasks { get; set; } = new();
    public ContractCatalog? ContractCatalog { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

/// <summary>
/// Main orchestrator for the "Coordinating Atomic Code Generation Tasks" workflow.
/// 
/// This orchestrator implements the pattern described in the problem statement:
/// 1. Planning First - Use LLM to clarify requirements and outline solution
/// 2. Gather Uncertainties - List unknowns and produce complete spec
/// 3. Break into Small Chunks - Turn spec into atomic subtasks
/// 4. Iterate and Verify - Run/review output after each task
/// 5. Shared Context - Maintain global context store for all tasks
/// </summary>
public class CoordinatedWorkflowOrchestrator
{
    private readonly OpenAIService _openAIService;
    private readonly PlanningService _planningService;
    private readonly ParallelExecutionEngine _executionEngine;
    private readonly CodeMergerService _mergerService;
    private readonly CodeValidatorService _validatorService;
    private readonly UserInteractionService _userInteractionService;
    private readonly ContractGenerationService? _contractService;
    private readonly WorkflowConfig _config;

    public CoordinatedWorkflowOrchestrator(
        OpenAIService openAIService,
        ParallelExecutionEngine executionEngine,
        CodeMergerService mergerService,
        CodeValidatorService validatorService,
        UserInteractionService userInteractionService,
        WorkflowConfig? config = null,
        ContractGenerationService? contractService = null)
    {
        _openAIService = openAIService;
        _planningService = new PlanningService(openAIService, userInteractionService);
        _executionEngine = executionEngine;
        _mergerService = mergerService;
        _validatorService = validatorService;
        _userInteractionService = userInteractionService;
        _contractService = contractService;
        _config = config ?? new WorkflowConfig();
    }

    /// <summary>
    /// Executes the complete coordinated workflow.
    /// </summary>
    public async Task<WorkflowResult> ExecuteAsync(string userRequest, string? outputDirectory = null)
    {
        var result = new WorkflowResult();
        var startTime = DateTime.UtcNow;
        var context = new SharedContext();

        try
        {
            Console.WriteLine();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     AoT Engine - Coordinated Atomic Code Generation       ║");
            Console.WriteLine("║                      Workflow                              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // ═══════════════════════════════════════════════════════════
            // PHASE 1: PLANNING FIRST - Gather Requirements
            // ═══════════════════════════════════════════════════════════
            if (_config.EnablePlanningPhase && _config.EnableClarificationPhase)
            {
                context = await _planningService.GatherRequirementsAsync(userRequest, context);
            }
            else
            {
                context.OriginalRequest = userRequest;
                Console.WriteLine("⏭️  Skipping clarification phase (disabled in config)");
            }

            // ═══════════════════════════════════════════════════════════
            // PHASE 2: GENERATE SPECIFICATION
            // ═══════════════════════════════════════════════════════════
            if (_config.EnablePlanningPhase)
            {
                context = await _planningService.GenerateSpecificationAsync(context);
            }
            else
            {
                Console.WriteLine("⏭️  Skipping specification phase (disabled in config)");
            }

            // ═══════════════════════════════════════════════════════════
            // PHASE 3: CREATE BLUEPRINT (Break into atomic tasks)
            // ═══════════════════════════════════════════════════════════
            if (_config.EnableBlueprintGeneration && _config.EnablePlanningPhase)
            {
                context = await _planningService.CreateBlueprintAsync(context, _config.MaxLinesPerTask);
            }
            else
            {
                Console.WriteLine("⏭️  Skipping blueprint phase (disabled in config)");
            }

            // ═══════════════════════════════════════════════════════════
            // PHASE 4: CONVERT BLUEPRINT TO TASK NODES
            // ═══════════════════════════════════════════════════════════
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("⚙️  PHASE 4: Converting Blueprint to Executable Tasks");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            List<TaskNode> tasks;
            if (context.Blueprint != null)
            {
                tasks = ConvertBlueprintToTasks(context.Blueprint, context);
            }
            else
            {
                // Fall back to direct decomposition if no blueprint
                var decomposition = await _openAIService.DecomposeTaskAsync(new TaskDecompositionRequest
                {
                    OriginalRequest = userRequest,
                    Context = BuildContextString(context)
                });
                tasks = decomposition.Tasks;
            }

            Console.WriteLine($"✓ Created {tasks.Count} executable tasks");

            // ═══════════════════════════════════════════════════════════
            // PHASE 5: CONTRACT-FIRST GENERATION (Optional)
            // ═══════════════════════════════════════════════════════════
            ContractCatalog? contractCatalog = null;
            if (_config.EnableContractFirst && _contractService != null)
            {
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("📋 PHASE 5: Contract-First Generation");
                Console.WriteLine("═══════════════════════════════════════════════════════════");

                contractCatalog = await _contractService.GenerateContractCatalogAsync(
                    tasks,
                    context.Blueprint?.ProjectName ?? "GeneratedProject",
                    userRequest);

                _openAIService.SetContractCatalog(
                    contractCatalog,
                    _contractService.SymbolTable,
                    _mergerService.TypeRegistry);

                result.ContractCatalog = contractCatalog;

                // Register contract types in shared context
                foreach (var contract in contractCatalog.Interfaces)
                {
                    context.RegisterType(new TypeDefinition
                    {
                        Name = contract.Name,
                        Namespace = contract.Namespace,
                        FullyQualifiedName = $"{contract.Namespace}.{contract.Name}",
                        Kind = "Interface",
                        Signature = contract.GenerateCode()
                    });
                }

                Console.WriteLine($"✓ Generated {contractCatalog.Interfaces.Count} interface contracts");
                Console.WriteLine($"✓ Generated {contractCatalog.Enums.Count} enum contracts");
                Console.WriteLine($"✓ Generated {contractCatalog.Models.Count} model contracts");
            }

            // ═══════════════════════════════════════════════════════════
            // PHASE 6: EXECUTE TASKS WITH ITERATIVE VERIFICATION
            // ═══════════════════════════════════════════════════════════
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("🚀 PHASE 6: Executing Tasks with Iterative Verification");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            if (_config.EnableIterativeVerification)
            {
                tasks = await ExecuteWithIterativeVerificationAsync(tasks, context, outputDirectory);
            }
            else
            {
                tasks = await _executionEngine.ExecuteTasksWithHybridValidationAsync(tasks);
            }

            // Store results in shared context
            foreach (var task in tasks.Where(t => t.IsCompleted))
            {
                context.IntermediateResults[task.Id] = new TaskResult
                {
                    TaskId = task.Id,
                    GeneratedCode = task.GeneratedCode,
                    IsValidated = task.IsValidated,
                    ValidationErrors = task.ValidationErrors,
                    AttemptCount = task.ValidationAttemptCount,
                    CompletedAtUtc = task.CompletedAtUtc ?? DateTime.UtcNow
                };
            }

            context.CreateCheckpoint("Task Execution", "Completed");

            // ═══════════════════════════════════════════════════════════
            // PHASE 7: VALIDATE CONTRACTS
            // ═══════════════════════════════════════════════════════════
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("✅ PHASE 7: Validating Contracts");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var contractValidation = _mergerService.ValidateContracts(tasks);
            if (!contractValidation.IsValid)
            {
                result.Success = false;
                result.ErrorMessage = $"Contract validation failed: {string.Join(", ", contractValidation.Errors)}";
                return result;
            }
            Console.WriteLine("✓ All contracts validated successfully");

            // ═══════════════════════════════════════════════════════════
            // PHASE 8: MERGE CODE
            // ═══════════════════════════════════════════════════════════
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("🔗 PHASE 8: Merging Code Snippets");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var mergedCode = await _mergerService.MergeCodeSnippetsAsync(tasks);
            result.FinalCode = mergedCode;
            Console.WriteLine($"✓ Merged code: {mergedCode.Split('\n').Length} lines");

            // ═══════════════════════════════════════════════════════════
            // PHASE 9: GENERATE REPORT
            // ═══════════════════════════════════════════════════════════
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("📊 PHASE 9: Generating Execution Report");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            result.ExecutionReport = GenerateWorkflowReport(context, tasks, mergedCode, startTime);
            result.Tasks = tasks;
            result.Context = context;
            result.Success = true;
            result.TotalDuration = DateTime.UtcNow - startTime;

            context.CreateCheckpoint("Workflow Complete", "Success");

            Console.WriteLine();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           ✅ Workflow Completed Successfully              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        }
        catch (OperationCanceledException ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.WriteLine($"\n⚠️  Workflow cancelled: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.TotalDuration = DateTime.UtcNow - startTime;
            Console.WriteLine($"\n❌ Workflow failed: {ex.Message}");
            Console.Error.WriteLine(ex.ToString());
        }

        return result;
    }

    /// <summary>
    /// Converts blueprint tasks to executable TaskNode objects.
    /// </summary>
    private List<TaskNode> ConvertBlueprintToTasks(ProjectBlueprint blueprint, SharedContext context)
    {
        var tasks = new List<TaskNode>();

        foreach (var blueprintTask in blueprint.Tasks)
        {
            var task = new TaskNode
            {
                Id = blueprintTask.Id,
                Description = blueprintTask.Description,
                Dependencies = blueprintTask.Dependencies,
                Namespace = blueprintTask.Component,
                ExpectedTypes = blueprintTask.ExpectedOutputs,
                Context = context.GetContextForTask(blueprintTask.Id, blueprintTask.Dependencies)
            };

            tasks.Add(task);
        }

        return tasks;
    }

    /// <summary>
    /// Executes tasks with iterative verification after each task.
    /// </summary>
    private async Task<List<TaskNode>> ExecuteWithIterativeVerificationAsync(
        List<TaskNode> tasks,
        SharedContext context,
        string? outputDirectory)
    {
        var completedTasks = new HashSet<string>();
        var taskMap = tasks.ToDictionary(t => t.Id);

        while (completedTasks.Count < tasks.Count)
        {
            // Find tasks that are ready (all dependencies completed)
            var readyTasks = tasks
                .Where(t => !completedTasks.Contains(t.Id) &&
                           t.Dependencies.All(d => completedTasks.Contains(d)))
                .ToList();

            if (!readyTasks.Any())
            {
                // Check for deadlock
                var pendingTasks = tasks.Where(t => !completedTasks.Contains(t.Id)).ToList();
                throw new InvalidOperationException(
                    $"Workflow deadlock: {pendingTasks.Count} tasks cannot be executed. " +
                    $"Pending: {string.Join(", ", pendingTasks.Select(t => t.Id))}");
            }

            // Execute ready tasks in parallel
            var executionTasks = readyTasks.Select(async task =>
            {
                Console.WriteLine($"\n▶️  Executing task: {task.Id}");
                Console.WriteLine($"   Description: {task.Description}");

                // Update task context with latest shared context
                task.Context = context.GetContextForTask(task.Id, task.Dependencies);

                // Generate code
                var completedTasksDict = context.IntermediateResults
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => tasks.First(t => t.Id == kvp.Key));
                task.GeneratedCode = await _openAIService.GenerateCodeAsync(task, completedTasksDict);

                // Verify immediately after generation
                Console.WriteLine($"   🔍 Verifying task {task.Id}...");
                var validation = await _validatorService.ValidateCodeAsync(task.GeneratedCode);

                int attempts = 1;
                while (!validation.IsValid && attempts < _config.MaxRetries)
                {
                    Console.WriteLine($"   ⚠️  Validation failed (attempt {attempts}), regenerating...");

                    task.GeneratedCode = await _openAIService.RegenerateCodeWithErrorsAsync(
                        task,
                        validation);

                    validation = await _validatorService.ValidateCodeAsync(task.GeneratedCode);
                    attempts++;
                }

                task.IsCompleted = true;
                task.IsValidated = validation.IsValid;
                task.ValidationErrors = validation.Errors;
                task.ValidationAttemptCount = attempts;
                task.CompletedAtUtc = DateTime.UtcNow;

                if (validation.IsValid)
                {
                    Console.WriteLine($"   ✅ Task {task.Id} completed and verified");

                    // Store in shared context for subsequent tasks
                    context.IntermediateResults[task.Id] = new TaskResult
                    {
                        TaskId = task.Id,
                        GeneratedCode = task.GeneratedCode,
                        IsValidated = true,
                        AttemptCount = attempts,
                        CompletedAtUtc = DateTime.UtcNow
                    };

                    // Extract and register types
                    ExtractAndRegisterTypes(task, context);
                }
                else
                {
                    Console.WriteLine($"   ❌ Task {task.Id} failed validation after {attempts} attempts");
                }

                return task;
            });

            await Task.WhenAll(executionTasks);

            foreach (var task in readyTasks)
            {
                completedTasks.Add(task.Id);
            }

            // Create checkpoint after each round
            context.CreateCheckpoint(
                $"Round {context.Checkpoints.Count(c => c.PhaseName.StartsWith("Round")) + 1}",
                $"Completed {completedTasks.Count}/{tasks.Count} tasks");
        }

        return tasks;
    }

    /// <summary>
    /// Extracts type definitions from generated code and registers them in context.
    /// Uses regular expressions for more robust parsing.
    /// </summary>
    private void ExtractAndRegisterTypes(TaskNode task, SharedContext context)
    {
        var code = task.GeneratedCode;
        var currentNamespace = task.Namespace;

        // Extract namespace
        var namespaceMatch = System.Text.RegularExpressions.Regex.Match(
            code, 
            @"namespace\s+([\w.]+)",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        
        if (namespaceMatch.Success)
        {
            currentNamespace = namespaceMatch.Groups[1].Value;
        }

        // Patterns for type declarations
        var typePatterns = new (string Pattern, string Kind)[]
        {
            (@"(?:public|internal|private|protected)?\s*(?:static|sealed|abstract|partial)?\s*interface\s+(\w+)", "Interface"),
            (@"(?:public|internal|private|protected)?\s*(?:static|sealed|abstract|partial)?\s*class\s+(\w+)", "Class"),
            (@"(?:public|internal|private|protected)?\s*(?:static|sealed|abstract|partial)?\s*struct\s+(\w+)", "Struct"),
            (@"(?:public|internal|private|protected)?\s*enum\s+(\w+)", "Enum"),
            (@"(?:public|internal|private|protected)?\s*record\s+(?:struct\s+)?(\w+)", "Record")
        };

        foreach (var (pattern, kind) in typePatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(
                code, 
                pattern,
                System.Text.RegularExpressions.RegexOptions.Multiline);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    var name = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(name))
                    {
                        context.RegisterType(new TypeDefinition
                        {
                            Name = name,
                            Namespace = currentNamespace,
                            FullyQualifiedName = $"{currentNamespace}.{name}",
                            Kind = kind,
                            DefinedByTaskId = task.Id
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Builds a context string from the shared context.
    /// </summary>
    private string BuildContextString(SharedContext context)
    {
        var sb = new System.Text.StringBuilder();

        if (context.ClarifiedRequirements.Any())
        {
            sb.AppendLine("Clarified Requirements:");
            foreach (var req in context.ClarifiedRequirements)
            {
                sb.AppendLine($"- {req.Question}: {req.Answer}");
            }
        }

        if (context.Specification != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Specification: {context.Specification.Summary}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a comprehensive workflow report.
    /// </summary>
    private string GenerateWorkflowReport(
        SharedContext context,
        List<TaskNode> tasks,
        string mergedCode,
        DateTime startTime)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║     Coordinated Workflow Execution Report                  ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine("=== WORKFLOW SUMMARY ===");
        sb.AppendLine($"Duration: {(DateTime.UtcNow - startTime).TotalSeconds:F1} seconds");
        sb.AppendLine($"Total Tasks: {tasks.Count}");
        sb.AppendLine($"Completed: {tasks.Count(t => t.IsCompleted)}");
        sb.AppendLine($"Validated: {tasks.Count(t => t.IsValidated)}");
        sb.AppendLine($"Checkpoints: {context.Checkpoints.Count}");
        sb.AppendLine();

        if (context.ClarifiedRequirements.Any())
        {
            sb.AppendLine("=== CLARIFIED REQUIREMENTS ===");
            foreach (var req in context.ClarifiedRequirements)
            {
                sb.AppendLine($"Q: {req.Question}");
                sb.AppendLine($"A: {req.Answer}");
                sb.AppendLine();
            }
        }

        if (context.Blueprint != null)
        {
            sb.AppendLine("=== BLUEPRINT ===");
            sb.AppendLine($"Project: {context.Blueprint.ProjectName}");
            sb.AppendLine($"Components: {string.Join(", ", context.Blueprint.Components)}");
            sb.AppendLine($"Tasks: {context.Blueprint.Tasks.Count}");
            sb.AppendLine();
        }

        sb.AppendLine("=== TASK EXECUTION DETAILS ===");
        foreach (var task in tasks.OrderBy(t => t.Id))
        {
            sb.AppendLine($"[{task.Id}] {task.Description}");
            sb.AppendLine($"    Status: {(task.IsCompleted ? "✓" : "✗")} Completed, {(task.IsValidated ? "✓" : "✗")} Validated");
            sb.AppendLine($"    Attempts: {task.ValidationAttemptCount}");
            if (task.ValidationErrors.Any())
            {
                sb.AppendLine($"    Errors: {string.Join(", ", task.ValidationErrors.Take(3))}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=== SHARED CONTEXT STATS ===");
        sb.AppendLine($"Types Registered: {context.TypeRegistry.Count}");
        sb.AppendLine($"Intermediate Results: {context.IntermediateResults.Count}");
        sb.AppendLine($"Uncertainties Resolved: {context.ResolvedUncertainties.Count}");
        sb.AppendLine();

        sb.AppendLine("=== FINAL CODE STATS ===");
        sb.AppendLine($"Lines: {mergedCode.Split('\n').Length}");
        sb.AppendLine($"Characters: {mergedCode.Length}");

        return sb.ToString();
    }
}
