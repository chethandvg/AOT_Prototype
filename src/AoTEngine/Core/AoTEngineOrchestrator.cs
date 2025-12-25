using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Core;

/// <summary>
/// Main orchestrator for the AoT (Atom of Thought) Engine.
/// </summary>
public class AoTEngineOrchestrator
{
    private readonly OpenAIService _openAIService;
    private readonly ParallelExecutionEngine _executionEngine;
    private readonly CodeMergerService _mergerService;
    private readonly UserInteractionService _userInteractionService;
    private readonly CodeValidatorService _validatorService;
    private readonly DocumentationService? _documentationService;
    private readonly DocumentationConfig? _documentationConfig;
    private const int DefaultMaxLineThreshold = 100;

    public AoTEngineOrchestrator(
        OpenAIService openAIService,
        ParallelExecutionEngine executionEngine,
        CodeMergerService mergerService,
        UserInteractionService userInteractionService,
        CodeValidatorService validatorService,
        DocumentationService? documentationService = null,
        DocumentationConfig? documentationConfig = null)
    {
        _openAIService = openAIService;
        _executionEngine = executionEngine;
        _mergerService = mergerService;
        _userInteractionService = userInteractionService;
        _validatorService = validatorService;
        _documentationService = documentationService;
        _documentationConfig = documentationConfig;
    }

    /// <summary>
    /// Executes the complete AoT workflow.
    /// </summary>
    /// <param name="userRequest">The user's request to execute.</param>
    /// <param name="context">Additional context for the request.</param>
    /// <param name="useBatchValidation">Whether to use batch validation mode.</param>
    /// <param name="useHybridValidation">Whether to use hybrid validation mode.</param>
    /// <param name="outputDirectory">Output directory for generated code.</param>
    /// <param name="maxLinesPerTask">Maximum lines per generated task (default: 100). Tasks exceeding this will be decomposed.</param>
    /// <param name="enableComplexityAnalysis">Whether to enable complexity analysis and automatic decomposition.</param>
    public async Task<AoTResult> ExecuteAsync(
        string userRequest, 
        string context = "", 
        bool useBatchValidation = true, 
        bool useHybridValidation = false,
        string? outputDirectory = null,
        int maxLinesPerTask = DefaultMaxLineThreshold,
        bool enableComplexityAnalysis = true)
    {
        var result = new AoTResult { OriginalRequest = userRequest };

        try
        {
            // Step 1: Decompose the request into atomic subtasks
            Console.WriteLine("Step 1: Decomposing request into atomic subtasks...");
            var decompositionRequest = new TaskDecompositionRequest
            {
                OriginalRequest = userRequest,
                Context = context
            };
            var decomposition = await _openAIService.DecomposeTaskAsync(decompositionRequest);
            result.Tasks = decomposition.Tasks;
            result.Description = decomposition.Description;

            // Review tasks with user and handle uncertainties
            result.Tasks = await _userInteractionService.ReviewTasksWithUserAsync(result.Tasks);

            Console.WriteLine($"Decomposed into {decomposition.Tasks.Count} tasks:");
            foreach (var task in decomposition.Tasks)
            {
                var deps = task.Dependencies.Any() ? string.Join(", ", task.Dependencies) : "None";
                Console.WriteLine($"  - {task.Id}: {task.Description} [Dependencies: {deps}]");
            }

            // Step 1.5: Complexity analysis and automatic decomposition of complex tasks
            if (enableComplexityAnalysis)
            {
                Console.WriteLine($"\nStep 1.5: Analyzing task complexity (max {maxLinesPerTask} lines per task)...");
                result.Tasks = await _executionEngine.AnalyzeAndDecomposeComplexTasksAsync(
                    result.Tasks, 
                    maxLinesPerTask);
                
                // Update decomposition tasks reference for downstream processing
                decomposition.Tasks = result.Tasks;
                
                if (result.Tasks.Count > decomposition.Tasks.Count)
                {
                    Console.WriteLine($"📋 Tasks after complexity analysis: {result.Tasks.Count}");
                }
            }

            // Step 2: Execute tasks - choose validation mode
            // Create a new execution engine instance with output directory if using batch/hybrid validation
            ParallelExecutionEngine executionEngine;
            
            if ((useHybridValidation || useBatchValidation) && !string.IsNullOrEmpty(outputDirectory))
            {
                Console.WriteLine($"\n📁 Output directory for build validation: {outputDirectory}");
                // Pass OpenAI service to ProjectBuildService for dynamic package version resolution
                var buildService = new ProjectBuildService(_openAIService);
                executionEngine = new ParallelExecutionEngine(
                    _openAIService, 
                    _validatorService,
                    _userInteractionService,
                    buildService,
                    outputDirectory,
                    _documentationService);
            }
            else
            {
                executionEngine = _executionEngine;
            }
            
            if (useHybridValidation)
            {
                Console.WriteLine("\nStep 2: Executing tasks with hybrid validation (individual + batch)...");
                result.Tasks = await executionEngine.ExecuteTasksWithHybridValidationAsync(result.Tasks);
            }
            else if (useBatchValidation)
            {
                Console.WriteLine("\nStep 2: Executing tasks with batch validation (inter-references will be resolved)...");
                result.Tasks = await executionEngine.ExecuteTasksWithBatchValidationAsync(result.Tasks);
            }
            else
            {
                Console.WriteLine("\nStep 2: Executing tasks in parallel with individual validation...");
                result.Tasks = await _executionEngine.ExecuteTasksAsync(result.Tasks);
            }

            // Step 3: Validate contracts
            Console.WriteLine("\nStep 3: Validating contracts...");
            var contractValidation = _mergerService.ValidateContracts(result.Tasks);
            if (!contractValidation.IsValid)
            {
                result.Success = false;
                result.ErrorMessage = $"Contract validation failed: {string.Join(", ", contractValidation.Errors)}";
                return result;
            }

            // Step 3.5: Validate integration across tasks (skip if batch or hybrid validation was used)
            if (!useBatchValidation && !useHybridValidation)
            {
                Console.WriteLine("\nStep 3.5: Validating code integration...");
                var integrationValidation = _validatorService.ValidateIntegration(result.Tasks);
                if (!integrationValidation.IsValid)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Integration validation failed: {string.Join(", ", integrationValidation.Errors)}";
                    return result;
                }
                if (integrationValidation.Warnings.Any())
                {
                    Console.WriteLine("⚠️  Integration warnings:");
                    foreach (var warning in integrationValidation.Warnings)
                    {
                        Console.WriteLine($"   {warning}");
                    }
                }
            }
            else
            {
                Console.WriteLine("\nStep 3.5: Skipping integration validation (already done in batch/hybrid validation)");
            }

            // Step 4: Merge code snippets
            Console.WriteLine("\nStep 4: Merging code snippets...");
            result.FinalCode = await _mergerService.MergeCodeSnippetsAsync(result.Tasks);

            // Step 5: Generate execution report
            Console.WriteLine("\nStep 5: Generating execution report...");
            result.ExecutionReport = _mergerService.CreateExecutionReport(result.Tasks, result.FinalCode);

            // Step 6: Synthesize documentation (if enabled)
            if (_documentationService != null && (_documentationConfig?.Enabled ?? true))
            {
                Console.WriteLine("\nStep 6: Synthesizing project documentation...");
                try
                {
                    result.ProjectDocumentation = await _documentationService.SynthesizeProjectDocumentationAsync(
                        result.Tasks,
                        userRequest,
                        result.Description);
                    
                    result.FinalDocumentation = GenerateMarkdownFromDocumentation(result.ProjectDocumentation);
                    
                    // Export documentation files if output directory is specified
                    if (!string.IsNullOrEmpty(outputDirectory))
                    {
                        result.DocumentationPaths = new DocumentationPaths();
                        
                        var docPath = Path.Combine(outputDirectory, "Documentation.md");
                        await _documentationService.ExportMarkdownAsync(result.ProjectDocumentation, docPath);
                        result.DocumentationPaths.MarkdownPath = docPath;
                        
                        var jsonPath = Path.Combine(outputDirectory, "Documentation.json");
                        await _documentationService.ExportJsonAsync(result.ProjectDocumentation, jsonPath);
                        result.DocumentationPaths.JsonPath = jsonPath;
                        
                        var jsonlPath = Path.Combine(outputDirectory, "training_data.jsonl");
                        await _documentationService.ExportJsonlDatasetAsync(result.ProjectDocumentation, jsonlPath);
                        result.DocumentationPaths.JsonlDatasetPath = jsonlPath;
                    }
                    
                    Console.WriteLine("✅ Documentation synthesis complete.");
                }
                catch (Exception docEx)
                {
                    // Documentation failure should not fail the overall execution
                    Console.WriteLine($"⚠️  Documentation synthesis failed: {docEx.Message}");
                }
            }

            result.Success = true;
            Console.WriteLine("\n✓ AoT Engine execution completed successfully!");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.WriteLine($"\n✗ AoT Engine execution failed: {ex.Message}");
            
            // Log full exception details for diagnostics (stack trace, inner exceptions, etc.)
            Console.Error.WriteLine("\nDetailed exception information:");
            Console.Error.WriteLine(ex.ToString());
        }

        return result;
    }
    
    /// <summary>
    /// Generates a simple markdown summary from project documentation.
    /// </summary>
    private string GenerateMarkdownFromDocumentation(ProjectDocumentation doc)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("# Project Documentation");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {doc.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(doc.HighLevelArchitectureSummary))
        {
            sb.AppendLine("## Architecture Overview");
            sb.AppendLine();
            sb.AppendLine(doc.HighLevelArchitectureSummary);
            sb.AppendLine();
        }
        
        sb.AppendLine("## Task Summaries");
        sb.AppendLine();
        
        foreach (var record in doc.TaskRecords)
        {
            sb.AppendLine($"### {record.TaskId}");
            sb.AppendLine($"**Purpose:** {record.Purpose}");
            if (!string.IsNullOrEmpty(record.ValidationNotes))
            {
                sb.AppendLine($"**Validation:** {record.ValidationNotes}");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Result of an AoT Engine execution.
/// </summary>
public class AoTResult
{
    /// <summary>
    /// The original user request.
    /// </summary>
    public string OriginalRequest { get; set; } = string.Empty;

    /// <summary>
    /// Description of the decomposition.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of executed tasks.
    /// </summary>
    public List<TaskNode> Tasks { get; set; } = new();

    /// <summary>
    /// Final merged code.
    /// </summary>
    public string FinalCode { get; set; } = string.Empty;

    /// <summary>
    /// Execution report.
    /// </summary>
    public string ExecutionReport { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Aggregated human-readable documentation for the project.
    /// </summary>
    public string FinalDocumentation { get; set; } = string.Empty;

    /// <summary>
    /// Complete project documentation with structured task summaries.
    /// </summary>
    public ProjectDocumentation? ProjectDocumentation { get; set; }

    /// <summary>
    /// Paths where documentation files were saved (if applicable).
    /// </summary>
    public DocumentationPaths? DocumentationPaths { get; set; }
}

/// <summary>
/// Contains paths to generated documentation files.
/// </summary>
public class DocumentationPaths
{
    /// <summary>
    /// Path to the markdown documentation file.
    /// </summary>
    public string? MarkdownPath { get; set; }

    /// <summary>
    /// Path to the JSON documentation file.
    /// </summary>
    public string? JsonPath { get; set; }

    /// <summary>
    /// Path to the JSONL training dataset file.
    /// </summary>
    public string? JsonlDatasetPath { get; set; }
}
