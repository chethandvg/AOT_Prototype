using AoTEngine.Models;
using AoTEngine.Services;
using Xunit;

namespace AoTEngine.Tests;

/// <summary>
/// Tests for error-based suggestion generation and duplicate type detection.
/// </summary>
public class RegenerationSuggestionsTests
{
    [Fact]
    public void GenerateSuggestionsFromErrors_MissingMember_ShouldSuggestAddingMember()
    {
        // Arrange
        var openAIService = new OpenAIService("test-key");
        var errors = new List<string>
        {
            "'MyClass' does not contain a definition for 'Execute'"
        };

        // Act - Using reflection to test private method
        var generateMethod = typeof(OpenAIService).GetMethod(
            "GenerateSuggestionsFromErrors",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var suggestions = generateMethod?.Invoke(openAIService, new object[] { errors }) as List<string>;

        // Assert
        Assert.NotNull(suggestions);
        Assert.Contains(suggestions, s => s.Contains("Execute") || s.Contains("MyClass"));
    }

    [Fact]
    public void GenerateSuggestionsFromErrors_InterfaceNotImplemented_ShouldSuggestImplementing()
    {
        // Arrange
        var openAIService = new OpenAIService("test-key");
        var errors = new List<string>
        {
            "'MyClass' does not implement interface member 'IService.Execute()'"
        };

        // Act
        var generateMethod = typeof(OpenAIService).GetMethod(
            "GenerateSuggestionsFromErrors",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var suggestions = generateMethod?.Invoke(openAIService, new object[] { errors }) as List<string>;

        // Assert
        Assert.NotNull(suggestions);
        Assert.Contains(suggestions, s => s.Contains("Implement") || s.Contains("IService"));
    }

    [Fact]
    public void GenerateSuggestionsFromErrors_TypeMismatch_ShouldSuggestConversion()
    {
        // Arrange
        var openAIService = new OpenAIService("test-key");
        var errors = new List<string>
        {
            "cannot implicitly convert type 'string' to 'int'"
        };

        // Act
        var generateMethod = typeof(OpenAIService).GetMethod(
            "GenerateSuggestionsFromErrors",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var suggestions = generateMethod?.Invoke(openAIService, new object[] { errors }) as List<string>;

        // Assert
        Assert.NotNull(suggestions);
        Assert.Contains(suggestions, s => s.Contains("Convert") || s.Contains("type"));
    }

    [Fact]
    public void GenerateSuggestionsFromErrors_MissingUsing_ShouldSuggestAddingDirective()
    {
        // Arrange
        var openAIService = new OpenAIService("test-key");
        var errors = new List<string>
        {
            "The type 'List<>' could not be found (are you missing a using directive?)"
        };

        // Act
        var generateMethod = typeof(OpenAIService).GetMethod(
            "GenerateSuggestionsFromErrors",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var suggestions = generateMethod?.Invoke(openAIService, new object[] { errors }) as List<string>;

        // Assert
        Assert.NotNull(suggestions);
        Assert.Contains(suggestions, s => s.Contains("using directive"));
    }

    [Fact]
    public void GenerateSuggestionsFromErrors_EmptyErrors_ShouldReturnEmptySuggestions()
    {
        // Arrange
        var openAIService = new OpenAIService("test-key");
        var errors = new List<string>();

        // Act
        var generateMethod = typeof(OpenAIService).GetMethod(
            "GenerateSuggestionsFromErrors",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var suggestions = generateMethod?.Invoke(openAIService, new object[] { errors }) as List<string>;

        // Assert
        Assert.NotNull(suggestions);
        Assert.Empty(suggestions);
    }

    [Fact]
    public void RegenerateCodeWithErrorsAsync_AcceptsOptionalSuggestions()
    {
        // Arrange
        var openAIService = new OpenAIService("test-key");
        var task = new TaskNode
        {
            Id = "task1",
            Description = "Test task",
            GeneratedCode = "public class Test { }"
        };
        
        var validationResult = new ValidationResult
        {
            IsValid = false,
            Errors = new List<string> { "Test error" }
        };
        
        var suggestions = new List<string> { "Try adding the missing method" };

        // Act & Assert - Just verify the method signature accepts the new parameters
        // Actual API call would require valid API key
        var method = typeof(OpenAIService).GetMethod("RegenerateCodeWithErrorsAsync");
        Assert.NotNull(method);
        
        var parameters = method.GetParameters();
        // Verify at least 4 parameters and check for the new optional parameters by name
        Assert.True(parameters.Length >= 4, "Method should have at least 4 parameters");
        Assert.Contains(parameters, p => p.Name == "suggestions");
        Assert.Contains(parameters, p => p.Name == "existingTypeCode");
    }
}
