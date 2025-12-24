using AoTEngine.Services;
using AoTEngine.Models;
using Xunit;

namespace AoTEngine.Tests;

public class CodeMergerServiceTests
{
    private readonly CodeMergerService _merger;
    private readonly CodeValidatorService _validator;

    public CodeMergerServiceTests()
    {
        _validator = new CodeValidatorService();
        _merger = new CodeMergerService(_validator);
    }

    [Fact]
    public async Task MergeCodeSnippetsAsync_WithMultipleTasks_ShouldMergeSuccessfully()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                Description = "Create calculator class",
                GeneratedCode = @"
using System;

namespace Calculator
{
    public class BasicCalculator
    {
        public int Add(int a, int b) => a + b;
    }
}",
                IsCompleted = true,
                IsValidated = true
            },
            new TaskNode
            {
                Id = "task2",
                Description = "Create advanced operations",
                GeneratedCode = @"
using System;

namespace Calculator
{
    public class AdvancedCalculator
    {
        public double Power(double baseNum, double exponent) => Math.Pow(baseNum, exponent);
    }
}",
                IsCompleted = true,
                IsValidated = true
            }
        };

        // Act
        var mergedCode = await _merger.MergeCodeSnippetsAsync(tasks);

        // Assert
        Assert.NotEmpty(mergedCode);
        Assert.Contains("using System;", mergedCode);
        Assert.Contains("namespace Calculator", mergedCode);
        Assert.Contains("BasicCalculator", mergedCode);
        Assert.Contains("AdvancedCalculator", mergedCode);
    }

    [Fact]
    public void ValidateContracts_WithAllDependenciesSatisfied_ShouldReturnValid()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                Description = "Task 1",
                IsValidated = true,
                Dependencies = new List<string>()
            },
            new TaskNode
            {
                Id = "task2",
                Description = "Task 2",
                IsValidated = true,
                Dependencies = new List<string> { "task1" }
            }
        };

        // Act
        var result = _merger.ValidateContracts(tasks);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateContracts_WithMissingDependency_ShouldReturnInvalid()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                Description = "Task 1",
                IsValidated = true,
                Dependencies = new List<string> { "task0" } // Missing dependency
            }
        };

        // Act
        var result = _merger.ValidateContracts(tasks);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void CreateExecutionReport_ShouldGenerateValidReport()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                Description = "Test task",
                IsCompleted = true,
                IsValidated = true,
                RetryCount = 0
            }
        };
        var mergedCode = "public class Test { }";

        // Act
        var report = _merger.CreateExecutionReport(tasks, mergedCode);

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("Total Tasks: 1", report);
        Assert.Contains("Completed Tasks: 1", report);
        Assert.Contains("Validated Tasks: 1", report);
        Assert.Contains("task1", report);
    }
}
