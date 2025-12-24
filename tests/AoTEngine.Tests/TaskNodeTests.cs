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
}
