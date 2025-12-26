using AoTEngine.Models;
using Xunit;

namespace AoTEngine.Tests;

public class TaskNodeTests
{
    [Fact]
    public void TaskNode_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var task = new TaskNode();

        // Assert
        Assert.NotNull(task.Id);
        Assert.NotNull(task.Description);
        Assert.NotNull(task.Dependencies);
        Assert.Empty(task.Dependencies);
        Assert.False(task.IsCompleted);
        Assert.False(task.IsValidated);
        Assert.Equal(0, task.RetryCount);
        
        // New documentation fields
        Assert.NotNull(task.Summary);
        Assert.Empty(task.Summary);
        Assert.NotNull(task.SummaryModel);
        Assert.Empty(task.SummaryModel);
        Assert.Equal(0, task.ValidationAttemptCount);
        Assert.Null(task.SummaryGeneratedAtUtc);
    }

    [Fact]
    public void TaskNode_ShouldSetProperties()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "task1",
            Description = "Test task",
            Dependencies = new List<string> { "task0" },
            Context = "Test context"
        };

        // Assert
        Assert.Equal("task1", task.Id);
        Assert.Equal("Test task", task.Description);
        Assert.Single(task.Dependencies);
        Assert.Equal("Test context", task.Context);
    }
    
    [Fact]
    public void TaskNode_ShouldSetDocumentationProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var task = new TaskNode
        {
            Id = "task1",
            Summary = "This task implements a calculator",
            SummaryModel = "gpt-4o-mini",
            ValidationAttemptCount = 2,
            SummaryGeneratedAtUtc = now
        };

        // Assert
        Assert.Equal("task1", task.Id);
        Assert.Equal("This task implements a calculator", task.Summary);
        Assert.Equal("gpt-4o-mini", task.SummaryModel);
        Assert.Equal(2, task.ValidationAttemptCount);
        Assert.Equal(now, task.SummaryGeneratedAtUtc);
    }

    [Fact]
    public void TaskNode_ShouldSetDocumentationStatus()
    {
        // Arrange & Act - Draft status
        var draftTask = new TaskNode
        {
            Id = "task1",
            Summary = "Implements the Test class",
            DocumentationStatus = "draft"
        };

        // Assert
        Assert.Equal("draft", draftTask.DocumentationStatus);

        // Arrange & Act - Final status
        var finalTask = new TaskNode
        {
            Id = "task2",
            Summary = "Implements the Helper class",
            DocumentationStatus = "final"
        };

        // Assert
        Assert.Equal("final", finalTask.DocumentationStatus);
    }

    [Fact]
    public void TaskNode_DocumentationStatus_DefaultsToEmptyString()
    {
        // Arrange & Act
        var task = new TaskNode();

        // Assert
        Assert.NotNull(task.DocumentationStatus);
        Assert.Empty(task.DocumentationStatus);
    }
}
