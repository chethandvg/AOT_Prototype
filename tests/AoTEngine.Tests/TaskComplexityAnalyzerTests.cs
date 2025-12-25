using AoTEngine.Models;
using AoTEngine.Services;
using Xunit;

namespace AoTEngine.Tests;

public class TaskComplexityAnalyzerTests
{
    private readonly TaskComplexityAnalyzer _analyzer;

    public TaskComplexityAnalyzerTests()
    {
        _analyzer = new TaskComplexityAnalyzer();
    }

    [Fact]
    public void AnalyzeTask_SimpleTask_ShouldNotRequireDecomposition()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "simple_task",
            Description = "Create a simple utility class",
            ExpectedTypes = new List<string> { "SimpleUtility" }
        };

        // Act
        var metrics = _analyzer.AnalyzeTask(task);

        // Assert
        Assert.False(metrics.RequiresDecomposition);
        Assert.True(metrics.EstimatedLineCount <= 100);
        Assert.Equal(1, metrics.ExpectedTypeCount);
    }

    [Fact]
    public void AnalyzeTask_ComplexTask_ShouldRequireDecomposition()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "complex_task",
            Description = "Implement a comprehensive CRUD service with validation, authentication, and caching for multiple entity types",
            ExpectedTypes = new List<string> { "UserService", "UserRepository", "UserValidator", "CacheService", "AuthService" },
            Dependencies = new List<string> { "task1", "task2", "task3" }
        };

        // Act
        var metrics = _analyzer.AnalyzeTask(task);

        // Assert
        Assert.True(metrics.RequiresDecomposition);
        Assert.True(metrics.EstimatedLineCount > 100);
        Assert.True(metrics.RecommendedSubtaskCount >= 2);
    }

    [Fact]
    public void AnalyzeTask_WithManyTypes_ShouldHaveHighTypeComplexity()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "multi_type_task",
            Description = "Create multiple models",
            ExpectedTypes = new List<string> { "Type1", "Type2", "Type3", "Type4", "Type5" }
        };

        // Act
        var metrics = _analyzer.AnalyzeTask(task);

        // Assert
        Assert.Equal(5, metrics.ExpectedTypeCount);
        Assert.True(metrics.Breakdown!.TypeComplexity > 0);
        Assert.True(metrics.RequiresDecomposition); // > 3 types should trigger decomposition
    }

    [Fact]
    public void AnalyzeTask_WithManyDependencies_ShouldHaveHighDependencyComplexity()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "dependent_task",
            Description = "Create a service",
            Dependencies = new List<string> { "dep1", "dep2", "dep3", "dep4" },
            ConsumedTypes = new Dictionary<string, List<string>>
            {
                { "dep1", new List<string> { "Type1", "Type2" } },
                { "dep2", new List<string> { "Type3", "Type4", "Type5" } }
            }
        };

        // Act
        var metrics = _analyzer.AnalyzeTask(task);

        // Assert
        Assert.Equal(4, metrics.DependencyCount);
        Assert.True(metrics.Breakdown!.DependencyComplexity > 0);
        Assert.True(metrics.Breakdown.ContributingFactors.Any(f => f.Contains("Multiple dependencies")));
    }

    [Fact]
    public void AnalyzeTask_ShouldCalculateComplexityScore()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "test_task",
            Description = "Implement a service with multiple methods",
            ExpectedTypes = new List<string> { "TestService" }
        };

        // Act
        var metrics = _analyzer.AnalyzeTask(task);

        // Assert
        Assert.InRange(metrics.ComplexityScore, 0, 100);
        Assert.NotNull(metrics.Breakdown);
    }

    [Fact]
    public void AnalyzeTask_ShouldHaveEstimationConfidence()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "test_task",
            Description = "Create a class with some methods",
            ExpectedTypes = new List<string> { "TestClass" }
        };

        // Act
        var metrics = _analyzer.AnalyzeTask(task);

        // Assert
        Assert.InRange(metrics.EstimationConfidence, 0.1, 0.9);
    }

    [Fact]
    public void AnalyzeTask_WithCRUDOperations_ShouldEstimateMoreMethods()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "crud_task",
            Description = "Implement CRUD operations for User entity"
        };

        // Act
        var metrics = _analyzer.AnalyzeTask(task);

        // Assert
        Assert.True(metrics.EstimatedMethodCount >= 4);
    }

    [Fact]
    public void AnalyzeTask_WithRepositoryPattern_ShouldEstimateMoreMethods()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "repo_task",
            Description = "Create a repository for managing user data"
        };

        // Act
        var metrics = _analyzer.AnalyzeTask(task);

        // Assert
        Assert.True(metrics.EstimatedMethodCount >= 5);
    }

    [Fact]
    public void AnalyzeTask_CustomThreshold_ShouldRespectThreshold()
    {
        // Arrange
        var task = new TaskNode
        {
            Id = "test_task",
            Description = "Create a simple class",
            ExpectedTypes = new List<string> { "SimpleClass" }
        };

        // Act
        var metricsDefault = _analyzer.AnalyzeTask(task, 100);
        var metricsLowThreshold = _analyzer.AnalyzeTask(task, 30);

        // Assert
        Assert.Equal(100, metricsDefault.MaxLineThreshold);
        Assert.Equal(30, metricsLowThreshold.MaxLineThreshold);
        // With low threshold, more tasks may require decomposition
    }

    [Fact]
    public void AnalyzeTasksForDecomposition_ShouldFilterComplexTasks()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode { Id = "simple1", Description = "Simple task" },
            new TaskNode { Id = "complex1", Description = "Complex comprehensive CRUD implementation", ExpectedTypes = new List<string> { "A", "B", "C", "D" } },
            new TaskNode { Id = "simple2", Description = "Another simple task" }
        };

        // Act
        var complexTasks = _analyzer.AnalyzeTasksForDecomposition(tasks);

        // Assert
        Assert.Single(complexTasks);
        Assert.Equal("complex1", complexTasks[0].Task.Id);
    }
}
