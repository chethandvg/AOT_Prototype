using AoTEngine.Core;
using AoTEngine.Models;

namespace AoTEngine.Tests;

public class DependencyGraphManagerV2Tests
{
    [Fact]
    public void AddDependency_ValidDependency_Succeeds()
    {
        // Arrange
        var manager = new DependencyGraphManagerV2();
        manager.RegisterTask("task1");
        manager.RegisterTask("task2");

        // Act
        var result = manager.AddDependency("task2", "task1");

        // Assert
        Assert.True(result.Success);
        Assert.False(result.CycleDetected);
    }

    [Fact]
    public void AddDependency_CyclicDependency_DetectsCycle()
    {
        // Arrange
        var manager = new DependencyGraphManagerV2();
        manager.RegisterTask("task1");
        manager.RegisterTask("task2");
        manager.AddDependency("task2", "task1");

        // Act
        var result = manager.AddDependency("task1", "task2");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.CycleDetected);
        Assert.NotEmpty(result.CyclePath);
    }

    [Fact]
    public void GetReadyTasks_NoDependencies_ReturnsTask()
    {
        // Arrange
        var manager = new DependencyGraphManagerV2();
        manager.RegisterTask("task1");

        // Act
        var readyTasks = manager.GetReadyTasks();

        // Assert
        Assert.Contains("task1", readyTasks);
    }

    [Fact]
    public void GetReadyTasks_WithDependencies_WaitsForCompletion()
    {
        // Arrange
        var manager = new DependencyGraphManagerV2();
        manager.RegisterTask("task1");
        manager.RegisterTask("task2");
        manager.AddDependency("task2", "task1");

        // Act - task2 should not be ready yet
        var readyTasks1 = manager.GetReadyTasks();
        
        // Complete task1
        manager.MarkCompleted("task1");
        
        // Act - task2 should now be ready
        var readyTasks2 = manager.GetReadyTasks();

        // Assert
        Assert.Contains("task1", readyTasks1);
        Assert.DoesNotContain("task2", readyTasks1);
        Assert.Contains("task2", readyTasks2);
    }

    [Fact]
    public void GenerateExecutionPlan_CreatesWaves()
    {
        // Arrange
        var manager = new DependencyGraphManagerV2();
        manager.RegisterTask("task1");
        manager.RegisterTask("task2");
        manager.RegisterTask("task3");
        manager.AddDependency("task3", "task1");
        manager.AddDependency("task3", "task2");

        // Act
        var plan = manager.GenerateExecutionPlan();

        // Assert
        Assert.NotEmpty(plan.Waves);
        Assert.True(plan.Waves.Count >= 2); // At least 2 waves
    }

    [Fact]
    public void MarkFailed_WithFailFastPolicy_FailsDependents()
    {
        // Arrange
        var manager = new DependencyGraphManagerV2(FailurePolicy.FailFast);
        manager.RegisterTask("task1");
        manager.RegisterTask("task2");
        manager.AddDependency("task2", "task1");

        // Act
        manager.MarkFailed("task1");
        var readyTasks = manager.GetReadyTasks();

        // Assert - task2 should not be ready because task1 failed
        Assert.DoesNotContain("task2", readyTasks);
    }
}
