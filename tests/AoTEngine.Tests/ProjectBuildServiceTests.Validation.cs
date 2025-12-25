using AoTEngine.Services;
using AoTEngine.Models;
using Xunit;

namespace AoTEngine.Tests;

/// <summary>
/// Partial class containing additional ProjectBuildService tests.
/// </summary>
public partial class ProjectBuildServiceTests
{
    [Fact]
    public async Task CreateProjectFromTasksAsync_WithEntryPoint_ShouldNotCreateDefaultProgram()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                Description = "Create entry point",
                Namespace = "MyApp",
                ExpectedTypes = new List<string> { "Program" },
                GeneratedCode = @"
using System;

namespace MyApp;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(""Hello, World!"");
    }
}",
                IsCompleted = true,
                IsValidated = true
            }
        };

        try
        {
            // Act
            var result = await _buildService.CreateProjectFromTasksAsync(_testOutputDirectory, "TestAppWithMain", tasks);

            // Assert
            Assert.True(result.Success, $"Build failed: {result.ErrorMessage}");
        }
        finally
        {
            // Cleanup handled by Dispose
        }
    }

    [Fact]
    public void ConvertToValidationResult_WithSuccessfulBuild_ShouldReturnValid()
    {
        // Arrange
        var buildResult = new ProjectBuildResult
        {
            Success = true,
            Errors = new List<string>(),
            Warnings = new List<string>()
        };

        // Act
        var validationResult = _buildService.ConvertToValidationResult(buildResult);

        // Assert
        Assert.True(validationResult.IsValid);
        Assert.Empty(validationResult.Errors);
    }

    [Fact]
    public void ConvertToValidationResult_WithFailedBuild_ShouldReturnInvalid()
    {
        // Arrange
        var buildResult = new ProjectBuildResult
        {
            Success = false,
            Errors = new List<string> { "CS0001: Syntax error" },
            Warnings = new List<string> { "CS0168: Variable declared but never used" }
        };

        // Act
        var validationResult = _buildService.ConvertToValidationResult(buildResult);

        // Assert
        Assert.False(validationResult.IsValid);
        Assert.NotEmpty(validationResult.Errors);
        Assert.NotEmpty(validationResult.Warnings);
    }
}
