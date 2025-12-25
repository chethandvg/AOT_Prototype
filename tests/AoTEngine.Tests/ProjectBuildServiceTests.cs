using AoTEngine.Services;
using AoTEngine.Models;
using Xunit;

namespace AoTEngine.Tests;

public class ProjectBuildServiceTests : IDisposable
{
    private readonly ProjectBuildService _buildService;
    private readonly string _testOutputDirectory;
    private bool _disposed;

    public ProjectBuildServiceTests()
    {
        _buildService = new ProjectBuildService();
        _testOutputDirectory = Path.Combine(Path.GetTempPath(), "AoTEngine_Tests", Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Cleanup test directory
                try
                {
                    if (Directory.Exists(_testOutputDirectory))
                    {
                        Directory.Delete(_testOutputDirectory, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
            _disposed = true;
        }
    }

    [Fact]
    public async Task CreateProjectFromTasksAsync_WithValidTasks_ShouldCreateSeparateFiles()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                Description = "Create calculator class",
                Namespace = "Calculator",
                ExpectedTypes = new List<string> { "BasicCalculator" },
                GeneratedCode = @"
using System;

namespace Calculator;

/// <summary>
/// A basic calculator class.
/// </summary>
public class BasicCalculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
}",
                IsCompleted = true,
                IsValidated = true
            },
            new TaskNode
            {
                Id = "task2",
                Description = "Create advanced calculator",
                Namespace = "Calculator",
                ExpectedTypes = new List<string> { "AdvancedCalculator" },
                GeneratedCode = @"
using System;

namespace Calculator;

/// <summary>
/// An advanced calculator class.
/// </summary>
public class AdvancedCalculator
{
    public double Power(double baseNum, double exponent) => Math.Pow(baseNum, exponent);
    public double SquareRoot(double value) => Math.Sqrt(value);
}",
                IsCompleted = true,
                IsValidated = true
            }
        };

        try
        {
            // Act
            var result = await _buildService.CreateProjectFromTasksAsync(_testOutputDirectory, "TestCalculator", tasks);

            // Assert
            Assert.True(result.Success, $"Build failed: {result.ErrorMessage}");
            Assert.NotEmpty(result.GeneratedFiles);
            Assert.True(result.GeneratedFiles.Count >= 2, "Should have created at least 2 code files");
            Assert.True(Directory.Exists(result.ProjectPath), "Project directory should exist");
            
            // Verify separate files were created
            Assert.True(result.GeneratedFiles.Any(f => f.Contains("BasicCalculator.cs")), "BasicCalculator.cs should exist");
            Assert.True(result.GeneratedFiles.Any(f => f.Contains("AdvancedCalculator.cs")), "AdvancedCalculator.cs should exist");
        }
        finally
        {
            // Cleanup handled by Dispose
        }
    }

    [Fact]
    public async Task CreateProjectFromTasksAsync_WithRequiredPackages_ShouldAddPackageReferences()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                Description = "Create JSON serializer",
                Namespace = "JsonExample",
                ExpectedTypes = new List<string> { "JsonHelper" },
                RequiredPackages = new List<string> { "Newtonsoft.Json:13.0.4" },
                GeneratedCode = @"
using System;
using Newtonsoft.Json;

namespace JsonExample;

public class JsonHelper
{
    public string Serialize<T>(T obj) => JsonConvert.SerializeObject(obj);
    public T? Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json);
}",
                IsCompleted = true,
                IsValidated = true
            }
        };

        try
        {
            // Act
            var result = await _buildService.CreateProjectFromTasksAsync(_testOutputDirectory, "TestJsonProject", tasks);

            // Assert
            Assert.True(result.Success, $"Build failed: {result.ErrorMessage}");
            
            // Verify package reference was added to .csproj
            var csprojPath = Path.Combine(result.ProjectPath, "TestJsonProject.csproj");
            Assert.True(File.Exists(csprojPath), "Project file should exist");
            
            var csprojContent = await File.ReadAllTextAsync(csprojPath);
            Assert.Contains("Newtonsoft.Json", csprojContent);
            
            // Verify version was added (escape dots properly in regex)
            Assert.Matches(@"Newtonsoft\.Json.*Version=""\d+\.\d+(\.\d+)?""", csprojContent);
        }
        finally
        {
            // Cleanup handled by Dispose
        }
    }

    [Fact]
    public async Task CreateProjectFromTasksAsync_WithUsingStatements_ShouldDetectAndAddPackages()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                Description = "Create logger service",
                Namespace = "LoggingExample",
                ExpectedTypes = new List<string> { "LoggerService" },
                // Note: RequiredPackages is empty, but the using statement should trigger package detection
                GeneratedCode = @"
using System;
using Microsoft.Extensions.Logging;

namespace LoggingExample;

public class LoggerService
{
    private readonly ILogger? _logger;
    
    public LoggerService(ILogger? logger = null)
    {
        _logger = logger;
    }
    
    public void Log(string message)
    {
        _logger?.LogInformation(message);
        Console.WriteLine(message);
    }
}",
                IsCompleted = true,
                IsValidated = true
            }
        };

        try
        {
            // Act
            var result = await _buildService.CreateProjectFromTasksAsync(_testOutputDirectory, "TestLoggingProject", tasks);

            // Assert
            Assert.True(result.Success, $"Build failed: {result.ErrorMessage}");
            
            // Verify package reference was detected and added to .csproj
            var csprojPath = Path.Combine(result.ProjectPath, "TestLoggingProject.csproj");
            Assert.True(File.Exists(csprojPath), "Project file should exist");
            
            var csprojContent = await File.ReadAllTextAsync(csprojPath);
            Assert.Contains("Microsoft.Extensions.Logging", csprojContent);
            
            // Verify that a version was added (escape dots properly in regex)
            Assert.Matches(@"Microsoft\.Extensions\.Logging.*Version=""\d+\.\d+(\.\d+)?""", csprojContent);
        }
        finally
        {
            // Cleanup handled by Dispose
        }
    }

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
