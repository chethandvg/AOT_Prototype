using AoTEngine.Services;
using Xunit;

namespace AoTEngine.Tests;

public class CodeValidatorServiceTests
{
    private readonly CodeValidatorService _validator;

    public CodeValidatorServiceTests()
    {
        _validator = new CodeValidatorService();
    }

    [Fact]
    public async Task ValidateCodeAsync_WithValidCode_ShouldReturnValid()
    {
        // Arrange
        var code = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}";

        // Act
        var result = await _validator.ValidateCodeAsync(code);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateCodeAsync_WithSyntaxError_ShouldReturnInvalid()
    {
        // Arrange
        var code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public int Add(int a, int b)
        {
            return a + b
        }
    }
}";

        // Act
        var result = await _validator.ValidateCodeAsync(code);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void LintCode_WithEmptyCode_ShouldReturnInvalid()
    {
        // Arrange
        var code = "";

        // Act
        var result = _validator.LintCode(code);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Fact]
    public void LintCode_WithNoNamespace_ShouldWarn()
    {
        // Arrange
        var code = "public class Test { }";

        // Act
        var result = _validator.LintCode(code);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("namespace"));
    }
}
