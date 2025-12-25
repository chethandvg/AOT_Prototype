using AoTEngine.Models;
using Newtonsoft.Json;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Service for managing checkpoint snapshots during execution.
/// Saves incremental checkpoints to track progress and enable recovery.
/// </summary>
public class CheckpointService
{
    private readonly string? _outputDirectory;

    public CheckpointService(string? outputDirectory)
    {
        _outputDirectory = outputDirectory;
    }

    /// <summary>
    /// Saves a checkpoint of the current execution state.
    /// Creates both JSON and Markdown formats.
    /// </summary>
    /// <param name="tasks">All tasks in the project.</param>
    /// <param name="completedTaskIds">IDs of completed tasks.</param>
    /// <param name="projectRequest">Original project request.</param>
    /// <param name="projectDescription">Project description from decomposition.</param>
    /// <param name="executionStatus">Current execution status.</param>
    /// <param name="architectureSummary">Architecture summary (optional).</param>
    /// <returns>Path to the saved checkpoint file, or null if saving failed.</returns>
    public async Task<string?> SaveCheckpointAsync(
        List<TaskNode> tasks,
        HashSet<string> completedTaskIds,
        string projectRequest,
        string projectDescription,
        string executionStatus = "in_progress",
        string architectureSummary = "")
    {
        if (string.IsNullOrEmpty(_outputDirectory))
        {
            return null; // No output directory specified
        }

        try
        {
            // Create checkpoints directory if it doesn't exist
            var checkpointsDir = Path.Combine(_outputDirectory, "checkpoints");
            Directory.CreateDirectory(checkpointsDir);

            // Create checkpoint data
            var checkpoint = CreateCheckpointData(
                tasks,
                completedTaskIds,
                projectRequest,
                projectDescription,
                executionStatus,
                architectureSummary);

            // Generate file name with timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var jsonFileName = $"checkpoint_{timestamp}.json";
            var mdFileName = $"checkpoint_{timestamp}.md";
            var jsonPath = Path.Combine(checkpointsDir, jsonFileName);
            var mdPath = Path.Combine(checkpointsDir, mdFileName);

            // Save JSON checkpoint
            var jsonContent = JsonConvert.SerializeObject(checkpoint, Formatting.Indented);
            await File.WriteAllTextAsync(jsonPath, jsonContent);

            // Save Markdown checkpoint
            var mdContent = GenerateCheckpointMarkdown(checkpoint);
            await File.WriteAllTextAsync(mdPath, mdContent);

            // Update "latest" symlink/copy
            await UpdateLatestCheckpoint(checkpointsDir, jsonPath, mdPath);

            Console.WriteLine($"💾 Checkpoint saved: {jsonFileName}");

            return jsonPath;
        }
        catch (Exception ex)
        {
            // Don't fail execution if checkpoint save fails
            Console.WriteLine($"⚠️  Failed to save checkpoint: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates checkpoint data from current execution state.
    /// </summary>
    private CheckpointData CreateCheckpointData(
        List<TaskNode> tasks,
        HashSet<string> completedTaskIds,
        string projectRequest,
        string projectDescription,
        string executionStatus,
        string architectureSummary)
    {
        var completedTasks = tasks.Where(t => completedTaskIds.Contains(t.Id)).ToList();
        // Pending tasks: not yet completed
        var pendingTasks = tasks.Where(t => !completedTaskIds.Contains(t.Id) && 
                                            (!t.ValidationErrors.Any() || t.IsValidated)).ToList();
        // Failed tasks: not completed and have validation errors that weren't resolved
        var failedTasks = tasks.Where(t => !completedTaskIds.Contains(t.Id) && 
                                           t.ValidationErrors.Any() && 
                                           !t.IsValidated).ToList();

        var checkpoint = new CheckpointData
        {
            CheckpointTimestamp = DateTime.UtcNow,
            ProjectRequest = projectRequest,
            ProjectDescription = projectDescription,
            TotalTasks = tasks.Count,
            CompletedTasks = completedTasks.Count,
            PendingTasks = pendingTasks.Count,
            FailedTasks = failedTasks.Count,
            PendingTaskIds = pendingTasks.Select(t => t.Id).ToList(),
            FailedTaskIds = failedTasks.Select(t => t.Id).ToList(),
            ArchitectureSummary = architectureSummary,
            ExecutionStatus = executionStatus
        };

        // Add completed task details
        foreach (var task in completedTasks)
        {
            checkpoint.CompletedTaskDetails.Add(new CompletedTaskDetail
            {
                TaskId = task.Id,
                Description = task.Description,
                Dependencies = task.Dependencies,
                ExpectedTypes = task.ExpectedTypes,
                Namespace = task.Namespace,
                GeneratedCode = task.GeneratedCode,
                ValidationStatus = task.IsValidated ? "validated" : "pending",
                ValidationAttempts = task.ValidationAttemptCount > 0 ? task.ValidationAttemptCount : task.RetryCount + 1,
                CompletedAt = DateTime.UtcNow,
                Summary = task.Summary
            });
        }

        // Build dependency graph
        foreach (var task in tasks)
        {
            checkpoint.DependencyGraph[task.Id] = task.Dependencies;
        }

        return checkpoint;
    }

    /// <summary>
    /// Generates a human-readable Markdown representation of the checkpoint.
    /// </summary>
    public string GenerateCheckpointMarkdown(CheckpointData checkpoint)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Execution Checkpoint");
        sb.AppendLine();
        sb.AppendLine($"**Timestamp:** {checkpoint.CheckpointTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Status:** {checkpoint.ExecutionStatus}");
        sb.AppendLine();

        // Project overview
        sb.AppendLine("## Project Overview");
        sb.AppendLine();
        sb.AppendLine($"**Request:** {checkpoint.ProjectRequest}");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(checkpoint.ProjectDescription))
        {
            sb.AppendLine($"**Description:** {checkpoint.ProjectDescription}");
            sb.AppendLine();
        }

        // Execution progress
        sb.AppendLine("## Execution Progress");
        sb.AppendLine();
        sb.AppendLine($"**Total Tasks:** {checkpoint.TotalTasks}");
        sb.AppendLine($"**Completed:** {checkpoint.CompletedTasks}");
        sb.AppendLine($"**Pending:** {checkpoint.PendingTasks}");
        sb.AppendLine($"**Failed:** {checkpoint.FailedTasks}");
        sb.AppendLine();

        var progressPercentage = checkpoint.TotalTasks > 0 
            ? (checkpoint.CompletedTasks * 100.0 / checkpoint.TotalTasks) 
            : 0;
        sb.AppendLine($"**Progress:** {progressPercentage:F1}% ({checkpoint.CompletedTasks}/{checkpoint.TotalTasks} tasks)");
        sb.AppendLine();

        // Completed tasks
        if (checkpoint.CompletedTaskDetails.Any())
        {
            sb.AppendLine("## Completed Tasks");
            sb.AppendLine();

            foreach (var task in checkpoint.CompletedTaskDetails)
            {
                sb.AppendLine($"### {task.TaskId}");
                sb.AppendLine();
                sb.AppendLine($"**Description:** {task.Description}");
                sb.AppendLine();

                if (task.Dependencies.Any())
                {
                    sb.AppendLine($"**Dependencies:** {string.Join(", ", task.Dependencies)}");
                    sb.AppendLine();
                }

                if (task.ExpectedTypes.Any())
                {
                    sb.AppendLine($"**Expected Types:** {string.Join(", ", task.ExpectedTypes)}");
                    sb.AppendLine();
                }

                if (!string.IsNullOrEmpty(task.Namespace))
                {
                    sb.AppendLine($"**Namespace:** {task.Namespace}");
                    sb.AppendLine();
                }

                sb.AppendLine($"**Validation Status:** {task.ValidationStatus}");
                sb.AppendLine($"**Validation Attempts:** {task.ValidationAttempts}");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(task.Summary))
                {
                    sb.AppendLine($"**Summary:** {task.Summary}");
                    sb.AppendLine();
                }

                sb.AppendLine("**Generated Code:**");
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(task.GeneratedCode);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        // Pending tasks
        if (checkpoint.PendingTaskIds.Any())
        {
            sb.AppendLine("## Pending Tasks");
            sb.AppendLine();
            foreach (var taskId in checkpoint.PendingTaskIds)
            {
                sb.AppendLine($"- {taskId}");
            }
            sb.AppendLine();
        }

        // Failed tasks
        if (checkpoint.FailedTaskIds.Any())
        {
            sb.AppendLine("## Failed Tasks");
            sb.AppendLine();
            foreach (var taskId in checkpoint.FailedTaskIds)
            {
                sb.AppendLine($"- {taskId}");
            }
            sb.AppendLine();
        }

        // Dependency graph
        sb.AppendLine("## Dependency Graph");
        sb.AppendLine();
        foreach (var kvp in checkpoint.DependencyGraph.OrderBy(x => x.Key))
        {
            var deps = kvp.Value.Any() ? string.Join(", ", kvp.Value) : "None";
            sb.AppendLine($"- **{kvp.Key}** → [{deps}]");
        }
        sb.AppendLine();

        // Architecture summary
        if (!string.IsNullOrEmpty(checkpoint.ArchitectureSummary))
        {
            sb.AppendLine("## Architecture Summary");
            sb.AppendLine();
            sb.AppendLine(checkpoint.ArchitectureSummary);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Loads a checkpoint from a file.
    /// </summary>
    /// <param name="filePath">Path to the checkpoint JSON file.</param>
    /// <returns>Loaded checkpoint data, or null if loading failed.</returns>
    public async Task<CheckpointData?> LoadCheckpointAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(filePath);
            return JsonConvert.DeserializeObject<CheckpointData>(jsonContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Failed to load checkpoint: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the path to the most recent checkpoint file.
    /// </summary>
    /// <param name="outputDirectory">Output directory containing checkpoints.</param>
    /// <returns>Path to the latest checkpoint, or null if none found.</returns>
    public string? GetLatestCheckpoint(string? outputDirectory)
    {
        if (string.IsNullOrEmpty(outputDirectory))
        {
            return null;
        }

        var checkpointsDir = Path.Combine(outputDirectory, "checkpoints");
        if (!Directory.Exists(checkpointsDir))
        {
            return null;
        }

        // Check for latest.json first
        var latestPath = Path.Combine(checkpointsDir, "latest.json");
        if (File.Exists(latestPath))
        {
            return latestPath;
        }

        // Otherwise, find the most recent checkpoint file
        var checkpointFiles = Directory.GetFiles(checkpointsDir, "checkpoint_*.json")
            .OrderByDescending(f => f)
            .ToList();

        return checkpointFiles.FirstOrDefault();
    }

    /// <summary>
    /// Updates the "latest" checkpoint files.
    /// </summary>
    private async Task UpdateLatestCheckpoint(string checkpointsDir, string jsonPath, string mdPath)
    {
        try
        {
            var latestJsonPath = Path.Combine(checkpointsDir, "latest.json");
            var latestMdPath = Path.Combine(checkpointsDir, "latest.md");

            // Copy files to "latest" versions
            File.Copy(jsonPath, latestJsonPath, overwrite: true);
            File.Copy(mdPath, latestMdPath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Don't fail if we can't update latest files
            Console.WriteLine($"⚠️  Failed to update latest checkpoint: {ex.Message}");
        }
    }
}
