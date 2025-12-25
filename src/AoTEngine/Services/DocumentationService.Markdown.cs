using AoTEngine.Models;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing markdown generation and architecture summary methods for documentation.
/// </summary>
public partial class DocumentationService
{
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
