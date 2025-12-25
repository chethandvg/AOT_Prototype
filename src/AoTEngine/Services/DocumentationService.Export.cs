using AoTEngine.Models;
using Newtonsoft.Json;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing export methods for documentation.
/// </summary>
public partial class DocumentationService
{
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
}
