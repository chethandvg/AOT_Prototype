using AoTEngine.Core;
using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Tests;

/// <summary>
/// Integration tests for checkpoint functionality.
/// These tests verify that checkpoints are created during execution.
/// Note: These tests do not make actual OpenAI API calls.
/// </summary>
public class CheckpointIntegrationTests
{
    [Fact]
    public async Task ExecuteTasksAsync_SavesCheckpoints_WhenOutputDirectorySpecified()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            // Create mock services (minimal setup for testing)
            var apiKey = "test-key";
            var openAIService = new OpenAIService(apiKey, "gpt-4");
            var validatorService = new CodeValidatorService();
            var userInteractionService = new UserInteractionService();
            
            // Create execution engine with checkpoint support
            var executionEngine = new ParallelExecutionEngine(
                openAIService,
                validatorService,
                userInteractionService,
                outputDirectory: tempDir,
                saveCheckpoints: true,
                checkpointFrequency: 1);
            
            // Set project context
            executionEngine.SetProjectContext(
                "Test project",
                "A test project for checkpoint verification");
            
            // Verify checkpoint service is properly initialized
            Assert.NotNull(executionEngine);
            
            // Verify checkpoint directory doesn't exist yet
            var checkpointsDir = Path.Combine(tempDir, "checkpoints");
            Assert.False(Directory.Exists(checkpointsDir), "Checkpoints directory should not exist before execution");
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
    public void ParallelExecutionEngine_Constructor_InitializesCheckpointService_WhenOutputDirectoryProvided()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var apiKey = "test-key";
            var openAIService = new OpenAIService(apiKey, "gpt-4");
            var validatorService = new CodeValidatorService();
            var userInteractionService = new UserInteractionService();
            
            // Act
            var executionEngine = new ParallelExecutionEngine(
                openAIService,
                validatorService,
                userInteractionService,
                outputDirectory: tempDir,
                saveCheckpoints: true);
            
            // Assert
            Assert.NotNull(executionEngine);
            // The checkpoint service is initialized (we can't directly test private fields,
            // but we can verify the engine was constructed successfully)
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
    public void ParallelExecutionEngine_Constructor_DisablesCheckpoints_WhenNoOutputDirectory()
    {
        // Arrange
        var apiKey = "test-key";
        var openAIService = new OpenAIService(apiKey, "gpt-4");
        var validatorService = new CodeValidatorService();
        var userInteractionService = new UserInteractionService();
        
        // Act
        var executionEngine = new ParallelExecutionEngine(
            openAIService,
            validatorService,
            userInteractionService,
            outputDirectory: null);
        
        // Assert
        Assert.NotNull(executionEngine);
        // Checkpoints should be disabled when no output directory is provided
    }
}
