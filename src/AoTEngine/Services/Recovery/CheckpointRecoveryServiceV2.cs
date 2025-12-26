using AoTEngine.Models;
using Newtonsoft.Json;

namespace AoTEngine.Services.Recovery;

/// <summary>
/// Service for checkpoint recovery with state consistency.
/// </summary>
public class CheckpointRecoveryServiceV2
{
    private readonly string _checkpointDirectory;
    private readonly TimeSpan _responseIdTtl;

    public CheckpointRecoveryServiceV2(
        string? checkpointDirectory = null,
        TimeSpan? responseIdTtl = null)
    {
        _checkpointDirectory = checkpointDirectory ?? Path.Combine(Path.GetTempPath(), "aot_checkpoints_v2");
        _responseIdTtl = responseIdTtl ?? TimeSpan.FromHours(24);

        if (!Directory.Exists(_checkpointDirectory))
        {
            Directory.CreateDirectory(_checkpointDirectory);
        }
    }

    /// <summary>
    /// Saves a checkpoint with serialization-safe snapshot.
    /// </summary>
    public async Task<string> SaveCheckpointAsync(SerializableTreeSnapshot snapshot, string? description = null)
    {
        var checkpoint = new Checkpoint
        {
            Snapshot = snapshot,
            Description = description ?? $"Checkpoint at depth {snapshot.MaxDepth}",
            Timestamp = DateTime.UtcNow
        };

        var filename = $"checkpoint_{checkpoint.Id}_{checkpoint.Timestamp:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine(_checkpointDirectory, filename);

        var json = JsonConvert.SerializeObject(checkpoint, Formatting.Indented);
        await File.WriteAllTextAsync(path, json);

        // Also save as latest
        var latestPath = Path.Combine(_checkpointDirectory, "latest.json");
        await File.WriteAllTextAsync(latestPath, json);

        return path;
    }

    /// <summary>
    /// Recovers from a checkpoint with response ID expiration handling.
    /// </summary>
    public async Task<RecoveryResult> RecoverFromCheckpointAsync(string checkpointPath)
    {
        var result = new RecoveryResult();

        try
        {
            if (!File.Exists(checkpointPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Checkpoint file not found: {checkpointPath}";
                return result;
            }

            var json = await File.ReadAllTextAsync(checkpointPath);
            var checkpoint = JsonConvert.DeserializeObject<Checkpoint>(json);

            if (checkpoint?.Snapshot == null)
            {
                result.Success = false;
                result.ErrorMessage = "Invalid checkpoint data";
                return result;
            }

            // Check for expired response IDs
            var nodesNeedingReconstruction = new List<string>();
            var cutoffTime = DateTime.UtcNow - _responseIdTtl;

            foreach (var node in checkpoint.Snapshot.AllNodes)
            {
                if (node.CreatedAt < cutoffTime && !string.IsNullOrEmpty(node.ResponseId))
                {
                    nodesNeedingReconstruction.Add(node.NodeId);
                    result.Warnings.Add($"Response ID for node {node.NodeId} may have expired");
                }
            }

            result.Success = true;
            result.RecoveredSnapshot = checkpoint.Snapshot;
            result.NodesNeedingReconstruction = nodesNeedingReconstruction;

            if (nodesNeedingReconstruction.Count > 0)
            {
                result.Warnings.Add($"{nodesNeedingReconstruction.Count} nodes may need context reconstruction");
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to recover checkpoint: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Recovers from the latest checkpoint.
    /// </summary>
    public async Task<RecoveryResult> RecoverFromLatestAsync()
    {
        var latestPath = Path.Combine(_checkpointDirectory, "latest.json");
        return await RecoverFromCheckpointAsync(latestPath);
    }

    /// <summary>
    /// Cleans up failed branch state.
    /// </summary>
    public void CleanupFailedBranch(string branchId)
    {
        // Remove checkpoints related to failed branch
        var files = Directory.GetFiles(_checkpointDirectory, $"*{branchId}*.json");
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    /// <summary>
    /// Rolls back to a previous contract version.
    /// </summary>
    public Task<bool> RollbackContractAsync(string contractKey, int targetVersion)
    {
        // Placeholder - not yet implemented
        throw new NotImplementedException("Contract rollback is not yet implemented. Store checkpoint data with contract versions to enable this feature.");
    }

    /// <summary>
    /// Generates a new branch ID for retry.
    /// </summary>
    public string GenerateNewBranchId()
    {
        return $"branch_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Lists all available checkpoints.
    /// </summary>
    public List<string> ListCheckpoints()
    {
        if (!Directory.Exists(_checkpointDirectory))
        {
            return new List<string>();
        }

        return Directory.GetFiles(_checkpointDirectory, "checkpoint_*.json")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .ToList();
    }
}
