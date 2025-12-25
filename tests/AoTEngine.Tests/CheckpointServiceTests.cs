using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Tests;

public class CheckpointServiceTests
{
    [Fact]
    public async Task SaveCheckpointAsync_CreatesJsonAndMarkdownFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var checkpointService = new CheckpointService(tempDir);
            var tasks = new List<TaskNode>
            {
                new TaskNode
                {
                    Id = "task1",
                    Description = "Create main class",
                    IsCompleted = true,
                    IsValidated = true,
                    GeneratedCode = "public class MyClass { }",
                    Namespace = "MyNamespace",
                    ExpectedTypes = new List<string> { "MyClass" },
                    ValidationAttemptCount = 1
                },
                new TaskNode
                {
                    Id = "task2",
                    Description = "Create helper method",
                    Dependencies = new List<string> { "task1" },
                    IsCompleted = false
                }
            };
            
            var completedTaskIds = new HashSet<string> { "task1" };
            
            // Act
            var checkpointPath = await checkpointService.SaveCheckpointAsync(
                tasks,
                completedTaskIds,
                "Create a simple C# application",
                "Application with main class and helper",
                "in_progress");
            
            // Assert
            Assert.NotNull(checkpointPath);
            Assert.True(File.Exists(checkpointPath), "JSON checkpoint should exist");
            
            var checkpointsDir = Path.Combine(tempDir, "checkpoints");
            Assert.True(Directory.Exists(checkpointsDir), "Checkpoints directory should exist");
            
            var mdFiles = Directory.GetFiles(checkpointsDir, "*.md");
            Assert.NotEmpty(mdFiles);
            
            var latestJson = Path.Combine(checkpointsDir, "latest.json");
            var latestMd = Path.Combine(checkpointsDir, "latest.md");
            Assert.True(File.Exists(latestJson), "latest.json should exist");
            Assert.True(File.Exists(latestMd), "latest.md should exist");
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
    public async Task LoadCheckpointAsync_LoadsCheckpointFromFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var checkpointService = new CheckpointService(tempDir);
            var tasks = new List<TaskNode>
            {
                new TaskNode
                {
                    Id = "task1",
                    Description = "Test task",
                    IsCompleted = true,
                    IsValidated = true,
                    GeneratedCode = "public class Test { }",
                    Namespace = "TestNamespace"
                }
            };
            
            var completedTaskIds = new HashSet<string> { "task1" };
            
            // Save checkpoint first
            var checkpointPath = await checkpointService.SaveCheckpointAsync(
                tasks,
                completedTaskIds,
                "Test request",
                "Test description");
            
            // Act
            var loadedCheckpoint = await checkpointService.LoadCheckpointAsync(checkpointPath!);
            
            // Assert
            Assert.NotNull(loadedCheckpoint);
            Assert.Equal("Test request", loadedCheckpoint.ProjectRequest);
            Assert.Equal("Test description", loadedCheckpoint.ProjectDescription);
            Assert.Equal(1, loadedCheckpoint.TotalTasks);
            Assert.Equal(1, loadedCheckpoint.CompletedTasks);
            Assert.Single(loadedCheckpoint.CompletedTaskDetails);
            Assert.Equal("task1", loadedCheckpoint.CompletedTaskDetails[0].TaskId);
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
    public void GetLatestCheckpoint_ReturnsLatestJsonFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var checkpointsDir = Path.Combine(tempDir, "checkpoints");
        Directory.CreateDirectory(checkpointsDir);
        
        try
        {
            var latestPath = Path.Combine(checkpointsDir, "latest.json");
            File.WriteAllText(latestPath, "{}");
            
            var checkpointService = new CheckpointService(tempDir);
            
            // Act
            var result = checkpointService.GetLatestCheckpoint(tempDir);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(latestPath, result);
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
    public void GenerateCheckpointMarkdown_CreatesValidMarkdown()
    {
        // Arrange
        var checkpointService = new CheckpointService(null);
        var checkpoint = new CheckpointData
        {
            ProjectRequest = "Test request",
            ProjectDescription = "Test description",
            TotalTasks = 2,
            CompletedTasks = 1,
            PendingTasks = 1,
            ExecutionStatus = "in_progress",
            CompletedTaskDetails = new List<CompletedTaskDetail>
            {
                new CompletedTaskDetail
                {
                    TaskId = "task1",
                    Description = "Test task",
                    GeneratedCode = "public class Test { }",
                    ValidationStatus = "validated",
                    ValidationAttempts = 1
                }
            },
            PendingTaskIds = new List<string> { "task2" },
            DependencyGraph = new Dictionary<string, List<string>>
            {
                { "task1", new List<string>() },
                { "task2", new List<string> { "task1" } }
            }
        };
        
        // Act
        var markdown = checkpointService.GenerateCheckpointMarkdown(checkpoint);
        
        // Assert
        Assert.Contains("# Execution Checkpoint", markdown);
        Assert.Contains("Test request", markdown);
        Assert.Contains("Test description", markdown);
        Assert.Contains("task1", markdown);
        Assert.Contains("task2", markdown);
        Assert.Contains("in_progress", markdown);
        Assert.Contains("public class Test { }", markdown);
    }
    
    [Fact]
    public async Task SaveCheckpointAsync_ReturnsNull_WhenNoOutputDirectory()
    {
        // Arrange
        var checkpointService = new CheckpointService(null);
        var tasks = new List<TaskNode>();
        var completedTaskIds = new HashSet<string>();
        
        // Act
        var result = await checkpointService.SaveCheckpointAsync(
            tasks,
            completedTaskIds,
            "Test",
            "Test");
        
        // Assert
        Assert.Null(result);
    }
}
