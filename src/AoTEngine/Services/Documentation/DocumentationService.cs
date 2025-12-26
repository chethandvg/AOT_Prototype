using AoTEngine.Models;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Service for generating and exporting documentation for AoT Engine tasks and projects.
/// This is the main partial class containing core fields and summary generation methods.
/// </summary>
/// <remarks>
/// This class is split into multiple partial class files for maintainability:
/// - DocumentationService.cs (this file): Core fields, constructor, and main summary methods
/// - DocumentationService.Export.cs: Export methods (JSON, Markdown, JSONL)
/// - DocumentationService.Utilities.cs: Utility methods (markdown generation, validation notes, etc.)
/// </remarks>
public partial class DocumentationService
{
    private readonly OpenAIService _openAIService;
    private readonly DocumentationConfig _config;
    
    /// <summary>
    /// Number of characters to use from the hash for code identification.
    /// </summary>
    private const int CodeHashLength = 16;
    
    /// <summary>
    /// Maximum number of validation errors to include in notes.
    /// </summary>
    private const int MaxValidationErrorsInNotes = 3;

    public DocumentationService(OpenAIService openAIService, DocumentationConfig? config = null)
    {
        _openAIService = openAIService;
        _config = config ?? new DocumentationConfig();
    }

    /// <summary>
    /// Generates a summary for a single task after its code has been validated.
    /// </summary>
    /// <param name="task">The task to generate a summary for.</param>
    /// <param name="dependencyTasks">Dictionary of dependency tasks for context.</param>
    /// <returns>A TaskSummaryRecord containing the structured summary.</returns>
    public async Task<TaskSummaryRecord> GenerateTaskSummaryAsync(
        TaskNode task, 
        Dictionary<string, TaskNode> dependencyTasks)
    {
        ArgumentNullException.ThrowIfNull(task);
        dependencyTasks ??= new Dictionary<string, TaskNode>();
        
        // Skip LLM call if no generated code exists
        if (string.IsNullOrWhiteSpace(task.GeneratedCode))
        {
            return CreateMinimalSummaryRecord(task);
        }
        
        if (!_config.Enabled || !_config.GeneratePerTask)
        {
            return CreateMinimalSummaryRecord(task);
        }

        var summaryRecord = new TaskSummaryRecord
        {
            TaskId = task.Id,
            TaskDescription = task.Description,
            Dependencies = new List<string>(task.Dependencies),
            ExpectedTypes = new List<string>(task.ExpectedTypes),
            Namespace = task.Namespace,
            GeneratedCode = task.GeneratedCode,
            GeneratedCodeHash = ComputeCodeHash(task.GeneratedCode),
            CreatedUtc = DateTime.UtcNow
        };

        // Build validation notes
        summaryRecord.ValidationNotes = BuildValidationNotes(task);

        try
        {
            // Generate summary using LLM
            var llmSummary = await _openAIService.GenerateTaskSummaryAsync(task, dependencyTasks);
            
            if (llmSummary != null)
            {
                summaryRecord.Purpose = llmSummary.Purpose;
                summaryRecord.KeyBehaviors = llmSummary.KeyBehaviors;
                summaryRecord.EdgeCases = llmSummary.EdgeCases;
                summaryRecord.SummaryModel = _config.SummaryModel;
                
                // Update task with summary
                task.Summary = llmSummary.Purpose;
                task.SummaryModel = _config.SummaryModel;
                task.SummaryGeneratedAtUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Failed to generate LLM summary for task {task.Id}: {ex.Message}");
            // Fall back to minimal summary
            summaryRecord.Purpose = $"Implements {task.Description}";
            task.Summary = summaryRecord.Purpose;
        }

        return summaryRecord;
    }

    /// <summary>
    /// Synthesizes project-level documentation from all task summaries.
    /// </summary>
    /// <param name="tasks">All tasks in the project.</param>
    /// <param name="originalRequest">The original user request.</param>
    /// <param name="description">Description from decomposition.</param>
    /// <returns>Complete project documentation.</returns>
    public async Task<ProjectDocumentation> SynthesizeProjectDocumentationAsync(
        List<TaskNode> tasks,
        string originalRequest,
        string description)
    {
        // Input validation
        if (tasks == null || tasks.Count == 0)
        {
            return new ProjectDocumentation
            {
                ProjectRequest = originalRequest ?? string.Empty,
                Description = description ?? string.Empty,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
        
        var doc = new ProjectDocumentation
        {
            ProjectRequest = originalRequest,
            Description = description,
            GeneratedAtUtc = DateTime.UtcNow
        };

        // Create task records from tasks
        foreach (var task in tasks.OrderBy(t => t.Id))
        {
            var record = new TaskSummaryRecord
            {
                TaskId = task.Id,
                TaskDescription = task.Description,
                Dependencies = new List<string>(task.Dependencies),
                ExpectedTypes = new List<string>(task.ExpectedTypes),
                Namespace = task.Namespace,
                Purpose = !string.IsNullOrEmpty(task.Summary) ? task.Summary : $"Implements {task.Description}",
                ValidationNotes = BuildValidationNotes(task),
                GeneratedCode = task.GeneratedCode,
                GeneratedCodeHash = ComputeCodeHash(task.GeneratedCode),
                SummaryModel = task.SummaryModel,
                CreatedUtc = task.SummaryGeneratedAtUtc ?? DateTime.UtcNow
            };
            doc.TaskRecords.Add(record);
        }

        // Build module index (type -> task mapping)
        doc.ModuleIndex = BuildModuleIndex(tasks);

        // Build dependency graph summary
        doc.DependencyGraphSummary = BuildDependencyGraphSummary(tasks);

        // Generate high-level architecture summary
        if (_config.GenerateProjectSummary)
        {
            try
            {
                doc.HighLevelArchitectureSummary = await GenerateArchitectureSummaryAsync(tasks, originalRequest, description);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Failed to generate architecture summary: {ex.Message}");
                doc.HighLevelArchitectureSummary = GenerateFallbackArchitectureSummary(tasks, description);
            }
        }
        else
        {
            doc.HighLevelArchitectureSummary = GenerateFallbackArchitectureSummary(tasks, description);
        }

        return doc;
    }
}

/// <summary>
/// Configuration for documentation generation.
/// </summary>
public class DocumentationConfig
{
    /// <summary>
    /// Whether documentation generation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to generate per-task summaries.
    /// </summary>
    public bool GeneratePerTask { get; set; } = true;

    /// <summary>
    /// Whether to generate project-level summary.
    /// </summary>
    public bool GenerateProjectSummary { get; set; } = true;

    /// <summary>
    /// Whether to export markdown documentation.
    /// </summary>
    public bool ExportMarkdown { get; set; } = true;

    /// <summary>
    /// Whether to export JSON documentation.
    /// </summary>
    public bool ExportJson { get; set; } = true;

    /// <summary>
    /// Whether to export JSONL training dataset.
    /// </summary>
    public bool ExportJsonl { get; set; } = true;

    /// <summary>
    /// Model to use for generating summaries.
    /// </summary>
    public string SummaryModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Maximum tokens for summary generation (currently not enforced in API calls).
    /// </summary>
    public int MaxSummaryTokens { get; set; } = 300;
}
