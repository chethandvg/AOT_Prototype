using AoTEngine.Models;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Service for generating and exporting documentation for AoT Engine tasks and projects.
/// </summary>
public class DocumentationService
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

    /// <summary>
    /// Exports project documentation to JSON format.
    /// </summary>
    /// <param name="doc">The project documentation to export.</param>
    /// <param name="path">The file path to write to.</param>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public async Task ExportJsonAsync(ProjectDocumentation doc, string path)
    {
        if (!_config.ExportJson) return;
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var json = JsonConvert.SerializeObject(doc, Formatting.Indented);
            await File.WriteAllTextAsync(path, json);
            Console.WriteLine($"   📄 Exported JSON documentation to: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            Console.WriteLine($"   ⚠️  Failed to export JSON documentation to {path}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Exports project documentation to Markdown format.
    /// </summary>
    /// <param name="doc">The project documentation to export.</param>
    /// <param name="path">The file path to write to.</param>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public async Task ExportMarkdownAsync(ProjectDocumentation doc, string path)
    {
        if (!_config.ExportMarkdown) return;
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var markdown = GenerateMarkdown(doc);
            await File.WriteAllTextAsync(path, markdown);
            Console.WriteLine($"   📄 Exported Markdown documentation to: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            Console.WriteLine($"   ⚠️  Failed to export Markdown documentation to {path}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Exports training dataset in JSONL format (one line per task).
    /// </summary>
    /// <param name="doc">The project documentation to export.</param>
    /// <param name="path">The file path to write to.</param>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public async Task ExportJsonlDatasetAsync(ProjectDocumentation doc, string path)
    {
        if (!_config.ExportJsonl) return;
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            // Stream JSONL lines directly to file for memory efficiency
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            
            foreach (var record in doc.TaskRecords)
            {
                var trainingRecord = new
                {
                    instruction = $"Generate C# code for: {record.TaskDescription}",
                    input = new
                    {
                        task_description = record.TaskDescription,
                        dependencies = record.Dependencies,
                        expected_types = record.ExpectedTypes,
                        @namespace = record.Namespace,
                        context = doc.ProjectRequest
                    },
                    output = record.GeneratedCode,
                    metadata = new
                    {
                        task_id = record.TaskId,
                        purpose = record.Purpose,
                        key_behaviors = record.KeyBehaviors,
                        edge_cases = record.EdgeCases,
                        validation_notes = record.ValidationNotes,
                        model_used = record.SummaryModel,
                        timestamp = record.CreatedUtc.ToString("o"),
                        code_hash = record.GeneratedCodeHash
                    }
                };
                
                await writer.WriteLineAsync(JsonConvert.SerializeObject(trainingRecord, Formatting.None));
            }
            
            Console.WriteLine($"   📄 Exported JSONL training dataset to: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            Console.WriteLine($"   ⚠️  Failed to export JSONL dataset to {path}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Generates markdown documentation from project documentation.
    /// </summary>
    private string GenerateMarkdown(ProjectDocumentation doc)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Project Documentation");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {doc.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        
        sb.AppendLine("## Original Request");
        sb.AppendLine();
        sb.AppendLine(doc.ProjectRequest);
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(doc.Description))
        {
            sb.AppendLine("## Description");
            sb.AppendLine();
            sb.AppendLine(doc.Description);
            sb.AppendLine();
        }
        
        if (!string.IsNullOrEmpty(doc.HighLevelArchitectureSummary))
        {
            sb.AppendLine("## Architecture Overview");
            sb.AppendLine();
            sb.AppendLine(doc.HighLevelArchitectureSummary);
            sb.AppendLine();
        }
        
        if (!string.IsNullOrEmpty(doc.DependencyGraphSummary))
        {
            sb.AppendLine("## Dependency Graph");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(doc.DependencyGraphSummary);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        
        sb.AppendLine("## Task Summaries");
        sb.AppendLine();
        
        foreach (var record in doc.TaskRecords)
        {
            sb.AppendLine($"### {record.TaskId}: {record.TaskDescription}");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(record.Namespace))
            {
                sb.AppendLine($"**Namespace:** `{record.Namespace}`");
                sb.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(record.Purpose))
            {
                sb.AppendLine($"**Purpose:** {record.Purpose}");
                sb.AppendLine();
            }
            
            if (record.Dependencies.Any())
            {
                sb.AppendLine($"**Dependencies:** {string.Join(", ", record.Dependencies)}");
                sb.AppendLine();
            }
            
            if (record.ExpectedTypes.Any())
            {
                sb.AppendLine($"**Types:** {string.Join(", ", record.ExpectedTypes)}");
                sb.AppendLine();
            }
            
            if (record.KeyBehaviors.Any())
            {
                sb.AppendLine("**Key Behaviors:**");
                foreach (var behavior in record.KeyBehaviors)
                {
                    sb.AppendLine($"- {behavior}");
                }
                sb.AppendLine();
            }
            
            if (record.EdgeCases.Any())
            {
                sb.AppendLine("**Edge Cases:**");
                foreach (var edgeCase in record.EdgeCases)
                {
                    sb.AppendLine($"- {edgeCase}");
                }
                sb.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(record.ValidationNotes))
            {
                sb.AppendLine($"**Validation:** {record.ValidationNotes}");
                sb.AppendLine();
            }
            
            sb.AppendLine("---");
            sb.AppendLine();
        }
        
        if (doc.ModuleIndex.Any())
        {
            sb.AppendLine("## Module Index");
            sb.AppendLine();
            sb.AppendLine("| Type | Task |");
            sb.AppendLine("|------|------|");
            foreach (var kvp in doc.ModuleIndex.OrderBy(k => k.Key))
            {
                sb.AppendLine($"| `{kvp.Key}` | {kvp.Value} |");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Creates a minimal summary record when LLM generation is disabled.
    /// </summary>
    private TaskSummaryRecord CreateMinimalSummaryRecord(TaskNode task)
    {
        return new TaskSummaryRecord
        {
            TaskId = task.Id,
            TaskDescription = task.Description,
            Dependencies = new List<string>(task.Dependencies),
            ExpectedTypes = new List<string>(task.ExpectedTypes),
            Namespace = task.Namespace,
            Purpose = $"Implements {task.Description}",
            ValidationNotes = BuildValidationNotes(task),
            GeneratedCode = task.GeneratedCode,
            GeneratedCodeHash = ComputeCodeHash(task.GeneratedCode),
            CreatedUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Builds validation notes from task state.
    /// </summary>
    private string BuildValidationNotes(TaskNode task)
    {
        var attemptCount = task.ValidationAttemptCount > 0 ? task.ValidationAttemptCount : task.RetryCount + 1;
        
        if (task.IsValidated)
        {
            if (attemptCount == 1)
            {
                return "Passed compilation on first attempt";
            }
            return $"Fixed after {attemptCount} attempt(s)";
        }
        
        if (task.ValidationErrors.Any())
        {
            return $"Validation issues: {string.Join("; ", task.ValidationErrors.Take(MaxValidationErrorsInNotes))}";
        }
        
        return "Pending validation";
    }

    /// <summary>
    /// Computes a hash of the generated code for tracking.
    /// </summary>
    private static string ComputeCodeHash(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        
        var bytes = Encoding.UTF8.GetBytes(code);
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash);
        var length = Math.Min(CodeHashLength, hex.Length);
        return hex[..length];
    }

    /// <summary>
    /// Builds a module index mapping types to their originating tasks.
    /// </summary>
    private Dictionary<string, string> BuildModuleIndex(List<TaskNode> tasks)
    {
        var index = new Dictionary<string, string>();
        
        foreach (var task in tasks)
        {
            foreach (var type in task.ExpectedTypes)
            {
                var key = !string.IsNullOrEmpty(task.Namespace) 
                    ? $"{task.Namespace}.{type}" 
                    : type;
                index[key] = task.Id;
            }
        }
        
        return index;
    }

    /// <summary>
    /// Builds a human-readable dependency graph summary.
    /// </summary>
    private string BuildDependencyGraphSummary(List<TaskNode> tasks)
    {
        var sb = new StringBuilder();
        
        // Group tasks by level (based on dependency depth)
        var levels = new Dictionary<int, List<TaskNode>>();
        var taskLevels = new Dictionary<string, int>();
        
        // Calculate levels
        foreach (var task in tasks)
        {
            var level = CalculateTaskLevel(task, tasks, taskLevels, new HashSet<string>());
            if (!levels.ContainsKey(level))
            {
                levels[level] = new List<TaskNode>();
            }
            levels[level].Add(task);
        }
        
        // Build graph representation
        foreach (var level in levels.OrderBy(l => l.Key))
        {
            sb.AppendLine($"Level {level.Key}:");
            foreach (var task in level.Value)
            {
                var deps = task.Dependencies.Any() 
                    ? $" → depends on: {string.Join(", ", task.Dependencies)}" 
                    : " (no dependencies)";
                sb.AppendLine($"  └─ {task.Id}: {task.Description}{deps}");
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Calculates the dependency level for a task.
    /// </summary>
    private int CalculateTaskLevel(TaskNode task, List<TaskNode> allTasks, Dictionary<string, int> cache, HashSet<string> visiting)
    {
        if (cache.TryGetValue(task.Id, out var cached))
        {
            return cached;
        }
        
        if (visiting.Contains(task.Id))
        {
            return 0; // Circular dependency, return 0
        }
        
        visiting.Add(task.Id);
        
        if (!task.Dependencies.Any())
        {
            cache[task.Id] = 0;
            visiting.Remove(task.Id);
            return 0;
        }
        
        var maxDepLevel = 0;
        foreach (var depId in task.Dependencies)
        {
            var depTask = allTasks.FirstOrDefault(t => t.Id == depId);
            if (depTask != null)
            {
                var depLevel = CalculateTaskLevel(depTask, allTasks, cache, visiting);
                maxDepLevel = Math.Max(maxDepLevel, depLevel);
            }
        }
        
        var level = maxDepLevel + 1;
        cache[task.Id] = level;
        visiting.Remove(task.Id);
        return level;
    }

    /// <summary>
    /// Generates a high-level architecture summary using LLM.
    /// </summary>
    private async Task<string> GenerateArchitectureSummaryAsync(
        List<TaskNode> tasks, 
        string originalRequest, 
        string description)
    {
        return await _openAIService.GenerateArchitectureSummaryAsync(tasks, originalRequest, description);
    }

    /// <summary>
    /// Generates a fallback architecture summary without LLM.
    /// </summary>
    private string GenerateFallbackArchitectureSummary(List<TaskNode> tasks, string description)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine(description);
        sb.AppendLine();
        sb.AppendLine($"The project consists of {tasks.Count} atomic tasks organized in a dependency graph.");
        
        // Group by namespace
        var namespaces = tasks
            .Where(t => !string.IsNullOrEmpty(t.Namespace))
            .GroupBy(t => t.Namespace)
            .ToList();
        
        if (namespaces.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**Namespaces:**");
            foreach (var ns in namespaces)
            {
                sb.AppendLine($"- `{ns.Key}`: {ns.Count()} task(s)");
            }
        }
        
        var rootTasks = tasks.Where(t => !t.Dependencies.Any()).ToList();
        if (rootTasks.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**Entry Points (no dependencies):**");
            foreach (var task in rootTasks)
            {
                sb.AppendLine($"- {task.Id}: {task.Description}");
            }
        }
        
        return sb.ToString();
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
