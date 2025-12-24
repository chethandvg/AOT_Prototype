using AoTEngine.Core;
using AoTEngine.Models;
using AoTEngine.Services;
using Xunit;

namespace AoTEngine.Tests;

public class ParallelExecutionEngineTests
{
    [Fact]
    public void TopologicalSort_WithNoDependencies_ShouldReturnAllTasks()
    {
        // Arrange
        var openAIService = new OpenAIService("test-key");
        var validatorService = new CodeValidatorService();
        var userInteractionService = new UserInteractionService();
        var engine = new ParallelExecutionEngine(openAIService, validatorService, userInteractionService);

        var tasks = new List<TaskNode>
        {
            new TaskNode { Id = "task1", Description = "Task 1" },
            new TaskNode { Id = "task2", Description = "Task 2" },
            new TaskNode { Id = "task3", Description = "Task 3" }
        };

        // Act
        var sorted = engine.TopologicalSort(tasks);

        // Assert
        Assert.Equal(3, sorted.Count);
    }

    [Fact]
    public void TopologicalSort_WithDependencies_ShouldOrderCorrectly()
    {
        // Arrange
        var openAIService = new OpenAIService("test-key");
        var validatorService = new CodeValidatorService();
        var userInteractionService = new UserInteractionService();
        var engine = new ParallelExecutionEngine(openAIService, validatorService, userInteractionService);

        var tasks = new List<TaskNode>
        {
            new TaskNode { Id = "task3", Description = "Task 3", Dependencies = new List<string> { "task1", "task2" } },
            new TaskNode { Id = "task1", Description = "Task 1" },
            new TaskNode { Id = "task2", Description = "Task 2", Dependencies = new List<string> { "task1" } }
        };

        // Act
        var sorted = engine.TopologicalSort(tasks);

        // Assert
        Assert.Equal(3, sorted.Count);
        Assert.Equal("task1", sorted[0].Id);
        Assert.Equal("task2", sorted[1].Id);
        Assert.Equal("task3", sorted[2].Id);
    }
}
