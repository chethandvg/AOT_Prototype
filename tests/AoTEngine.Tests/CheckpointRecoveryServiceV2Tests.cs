using AoTEngine.Models;
using AoTEngine.Services.Recovery;

namespace AoTEngine.Tests;

public class CheckpointRecoveryServiceV2Tests
{
    [Fact]
    public async Task SaveCheckpoint_CreatesCheckpointFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"aot_test_{Guid.NewGuid():N}");
        var service = new CheckpointRecoveryServiceV2(tempDir);
        var snapshot = new SerializableTreeSnapshot
        {
            Root = new ResponseChainNode { NodeId = "root" },
            MaxDepth = 3,
            AtomicNodeCount = 5
        };

        try
        {
            // Act
            var checkpointPath = await service.SaveCheckpointAsync(snapshot, "Test checkpoint");

            // Assert
            Assert.True(File.Exists(checkpointPath));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task RecoverFromCheckpoint_ValidCheckpoint_Succeeds()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"aot_test_{Guid.NewGuid():N}");
        var service = new CheckpointRecoveryServiceV2(tempDir);
        var snapshot = new SerializableTreeSnapshot
        {
            Root = new ResponseChainNode { NodeId = "root" },
            MaxDepth = 3,
            AtomicNodeCount = 5
        };

        try
        {
            var checkpointPath = await service.SaveCheckpointAsync(snapshot);

            // Act
            var result = await service.RecoverFromCheckpointAsync(checkpointPath);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.RecoveredSnapshot);
            Assert.Equal(3, result.RecoveredSnapshot.MaxDepth);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task RecoverFromCheckpoint_InvalidPath_Fails()
    {
        // Arrange
        var service = new CheckpointRecoveryServiceV2();
        var invalidPath = "/nonexistent/path/checkpoint.json";

        // Act
        var result = await service.RecoverFromCheckpointAsync(invalidPath);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void GenerateNewBranchId_ReturnsValidId()
    {
        // Arrange
        var service = new CheckpointRecoveryServiceV2();

        // Act
        var branchId1 = service.GenerateNewBranchId();
        var branchId2 = service.GenerateNewBranchId();

        // Assert
        Assert.NotNull(branchId1);
        Assert.NotNull(branchId2);
        Assert.NotEqual(branchId1, branchId2);
        Assert.StartsWith("branch_", branchId1);
    }
}
