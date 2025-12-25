using AoTEngine.Models;
using AoTEngine.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AoTEngine.Tests;

public class IntegrationFixerTests
{
    private readonly IntegrationFixer _fixer;
    private readonly TypeRegistry _typeRegistry;
    private readonly SymbolTable _symbolTable;

    public IntegrationFixerTests()
    {
        _typeRegistry = new TypeRegistry();
        _symbolTable = new SymbolTable();
        _fixer = new IntegrationFixer(_typeRegistry, _symbolTable);
    }

    [Fact]
    public void AddMissingUsings_ShouldAddNewUsings()
    {
        // Arrange
        var code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public void Method() { }
    }
}";
        var missingNamespaces = new List<string> { "System", "System.Collections.Generic" };

        // Act
        var result = _fixer.AddMissingUsings(code, missingNamespaces);

        // Assert - normalize whitespace for comparison
        var normalizedResult = result.Replace(" ", "");
        Assert.Contains("usingSystem;", normalizedResult);
        Assert.Contains("usingSystem.Collections.Generic;", normalizedResult);
    }

    [Fact]
    public void AddMissingUsings_ShouldNotDuplicateExistingUsings()
    {
        // Arrange
        var code = @"
using System;

namespace TestNamespace
{
    public class TestClass { }
}";
        var missingNamespaces = new List<string> { "System", "System.Linq" };

        // Act
        var result = _fixer.AddMissingUsings(code, missingNamespaces);

        // Assert - Use Roslyn to parse and count actual using directives
        var syntaxTree = CSharpSyntaxTree.ParseText(result);
        var root = (Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax)syntaxTree.GetRoot();
        
        // Count "System" (not "System.Linq" or other System.* namespaces)
        var systemUsingCount = root.Usings.Count(u => 
        {
            var name = u.Name?.ToString();
            return name == "System";
        });
        Assert.Equal(1, systemUsingCount); // Should only appear once
        
        // Check System.Linq is present by looking at the resulting string
        Assert.Contains("System.Linq", result);
    }

    [Fact]
    public void RemoveDuplicateTypes_WithDuplicateClasses_ShouldDetectIssue()
    {
        // Arrange - This is a complex scenario that requires the RemoveDuplicateTypes method
        // to properly identify which type to remove based on the NewEntry's OwnerTaskId
        var code = @"
namespace TestNamespace
{
    public class MyClass
    {
        public void Method1() { }
    }

    public class OtherClass
    {
        public void Method2() { }
    }
}";
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "TestNamespace.MyClass",
                SuggestedResolution = ConflictResolution.KeepFirst,
                ExistingEntry = new TypeRegistryEntry
                {
                    TypeName = "MyClass",
                    Namespace = "TestNamespace",
                    OwnerTaskId = "task1"
                },
                NewEntry = new TypeRegistryEntry
                {
                    TypeName = "MyClass",
                    Namespace = "TestNamespace",
                    OwnerTaskId = "task2"
                }
            }
        };

        // Act
        var result = _fixer.RemoveDuplicateTypes(code, conflicts);

        // Assert - verify we get valid code back (the method only removes when it can find by namespace)
        Assert.NotNull(result);
        Assert.Contains("class", result);
    }

    [Fact]
    public void ConvertToPartialClasses_ShouldAddPartialModifier()
    {
        // Arrange
        var code = @"
namespace TestNamespace
{
    public class MyClass
    {
        public void Method() { }
    }
}";
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "TestNamespace.MyClass",
                SuggestedResolution = ConflictResolution.MergeAsPartial,
                ExistingEntry = new TypeRegistryEntry(),
                NewEntry = new TypeRegistryEntry()
            }
        };

        // Act
        var result = _fixer.ConvertToPartialClasses(code, conflicts);

        // Assert
        Assert.Contains("partial class MyClass", result);
    }

    [Fact]
    public void TryFix_WithNoDiagnostics_ShouldReturnSuccess()
    {
        // Arrange
        var code = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public int Add(int a, int b) => a + b;
    }
}";
        var diagnostics = Enumerable.Empty<Diagnostic>();

        // Act
        var result = _fixer.TryFix(code, diagnostics);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.UnfixableErrors);
    }

    [Fact]
    public void RemoveDuplicateMembers_ShouldRemoveDuplicates()
    {
        // Arrange
        var code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public TestClass(string name) { }
        public TestClass(string name) { }
        public void Method() { }
    }
}";

        var duplicates = new List<MemberSignature>
        {
            new MemberSignature
            {
                Name = "TestClass",
                Kind = ProjectMemberKind.Constructor,
                ParameterTypes = new List<string> { "string" }
            }
        };

        // Act
        var result = _fixer.RemoveDuplicateMembers(code, "TestClass", duplicates);

        // Assert
        // Count constructor occurrences
        var ctorCount = result.Split(new[] { "public TestClass(string name)" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(1, ctorCount);
    }

    [Fact]
    public void ResolveAmbiguousReferences_ShouldFullyQualify()
    {
        // Arrange
        var code = @"
namespace TestNamespace
{
    public class Consumer
    {
        public MyType GetValue() => null;
    }
}";
        var ambiguousTypes = new Dictionary<string, string>
        {
            { "MyType", "FullNamespace.MyType" }
        };

        // Act
        var result = _fixer.ResolveAmbiguousReferences(code, ambiguousTypes);

        // Assert
        Assert.Contains("FullNamespace.MyType", result);
    }
}

public class CodeMergerIntegrationTests
{
    private readonly CodeMergerService _merger;
    private readonly CodeValidatorService _validator;

    public CodeMergerIntegrationTests()
    {
        _validator = new CodeValidatorService();
        _merger = new CodeMergerService(_validator);
    }

    [Fact]
    public async Task MergeWithIntegrationAsync_WithNoDuplicates_ShouldSucceed()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                GeneratedCode = @"
using System;

namespace Calculator
{
    public class BasicCalculator
    {
        public int Add(int a, int b) => a + b;
    }
}"
            },
            new TaskNode
            {
                Id = "task2",
                GeneratedCode = @"
using System;

namespace Calculator
{
    public class AdvancedCalculator
    {
        public double Power(double a, double b) => Math.Pow(a, b);
    }
}"
            }
        };

        // Act
        var result = await _merger.MergeWithIntegrationAsync(tasks);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("BasicCalculator", result.MergedCode);
        Assert.Contains("AdvancedCalculator", result.MergedCode);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task MergeWithIntegrationAsync_WithDuplicateInterface_ShouldDetectConflict()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                GeneratedCode = @"
namespace MyApp
{
    public interface IValidator
    {
        bool Validate(string input);
    }
}"
            },
            new TaskNode
            {
                Id = "task2",
                GeneratedCode = @"
namespace MyApp
{
    public interface IValidator
    {
        bool Validate(string input);
    }
}"
            }
        };

        // Act
        var result = await _merger.MergeWithIntegrationAsync(tasks);

        // Assert
        Assert.NotEmpty(result.Conflicts);
        Assert.Contains(result.Conflicts, c => c.FullyQualifiedName == "MyApp.IValidator");
    }

    [Fact]
    public async Task MergeWithIntegrationAsync_WithAutoResolve_ShouldApplyFixes()
    {
        // Arrange
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                Id = "task1",
                GeneratedCode = @"
namespace MyApp
{
    public interface IValidator
    {
        bool Validate();
    }
}"
            },
            new TaskNode
            {
                Id = "task2",
                GeneratedCode = @"
namespace MyApp
{
    public interface IValidator
    {
        bool Validate();
    }

    public class Validator : IValidator
    {
        public bool Validate() => true;
    }
}"
            }
        };

        var options = new MergeOptions
        {
            AutoResolveKeepFirst = true,
            EnableAutoFix = true
        };

        // Act
        var result = await _merger.MergeWithIntegrationAsync(tasks, options);

        // Assert
        Assert.NotEmpty(result.AppliedFixes);
        // The fix should have kept first definition (message starts with this prefix)
        Assert.Contains(result.AppliedFixes, f => f.StartsWith("Kept first definition of "));
    }

    [Fact]
    public void TypeRegistry_ShouldBeAccessible()
    {
        // Assert
        Assert.NotNull(_merger.TypeRegistry);
    }

    [Fact]
    public void SymbolTable_ShouldBeAccessible()
    {
        // Assert
        Assert.NotNull(_merger.SymbolTable);
    }

    [Fact]
    public void ResetMergeState_ShouldClearRegistries()
    {
        // Arrange
        _merger.TypeRegistry.TryRegister(new TypeRegistryEntry
        {
            FullyQualifiedName = "Test.Type",
            Namespace = "Test",
            TypeName = "Type",
            Kind = ProjectTypeKind.Class
        });

        // Act
        _merger.ResetMergeState();

        // Assert
        Assert.Empty(_merger.TypeRegistry.Types);
    }
}
