using AoTEngine.Models;
using AoTEngine.Services;
using System.Linq;
using Xunit;

namespace AoTEngine.Tests;

/// <summary>
/// Tests for PromptContextBuilder service.
/// </summary>
public class PromptContextBuilderTests
{
    [Fact]
    public void BuildCodeGenerationContext_IncludesGuardrails()
    {
        // Arrange
        var catalog = new ContractCatalog { IsFrozen = true };
        var symbolTable = new SymbolTable();
        var typeRegistry = new TypeRegistry();
        var builder = new PromptContextBuilder(catalog, symbolTable, typeRegistry);
        
        var task = new TaskNode
        {
            Id = "task1",
            Description = "Implement data service",
            Namespace = "TestProject.Services",
            ExpectedTypes = new List<string> { "DataService" }
        };

        // Act
        var context = builder.BuildCodeGenerationContext(task, new Dictionary<string, TaskNode>());

        // Assert
        Assert.Contains("CRITICAL RULES", context);
        Assert.Contains("DO NOT redefine", context);
        Assert.Contains("IMPLEMENT all interface members", context);
    }

    [Fact]
    public void BuildCodeGenerationContext_IncludesTaskDetails()
    {
        // Arrange
        var builder = new PromptContextBuilder(null, new SymbolTable(), new TypeRegistry());
        
        var task = new TaskNode
        {
            Id = "task1",
            Description = "Create user service",
            Namespace = "MyProject.Services",
            ExpectedTypes = new List<string> { "UserService", "IUserService" },
            Context = "Handle user CRUD operations"
        };

        // Act
        var context = builder.BuildCodeGenerationContext(task, new Dictionary<string, TaskNode>());

        // Assert
        Assert.Contains("Task ID: task1", context);
        Assert.Contains("Create user service", context);
        Assert.Contains("MyProject.Services", context);
        Assert.Contains("UserService", context);
        Assert.Contains("Handle user CRUD operations", context);
    }

    [Fact]
    public void BuildCodeGenerationContext_IncludesKnownTypesFromSymbolTable()
    {
        // Arrange
        var symbolTable = new SymbolTable();
        symbolTable.TryRegister(new ProjectSymbolInfo
        {
            Name = "IExistingService",
            Namespace = "TestProject.Services",
            FullyQualifiedName = "TestProject.Services.IExistingService",
            Kind = ProjectSymbolKind.Interface
        });
        
        var builder = new PromptContextBuilder(null, symbolTable, new TypeRegistry());
        
        var task = new TaskNode { Id = "task1", Description = "Test" };

        // Act
        var context = builder.BuildCodeGenerationContext(task, new Dictionary<string, TaskNode>());

        // Assert
        Assert.Contains("Existing types", context);
        Assert.Contains("IExistingService", context);
    }

    [Fact]
    public void BuildCodeGenerationContext_IncludesAmbiguityWarnings()
    {
        // Arrange
        var symbolTable = new SymbolTable();
        var typeRegistry = new TypeRegistry();
        
        // Register same type in two namespaces
        typeRegistry.TryRegister(new TypeRegistryEntry
        {
            TypeName = "Result",
            Namespace = "TestProject.Models",
            FullyQualifiedName = "TestProject.Models.Result",
            Kind = ProjectTypeKind.Class
        });
        typeRegistry.TryRegister(new TypeRegistryEntry
        {
            TypeName = "Result",
            Namespace = "TestProject.Services",
            FullyQualifiedName = "TestProject.Services.Result",
            Kind = ProjectTypeKind.Class
        });
        
        var builder = new PromptContextBuilder(null, symbolTable, typeRegistry);
        
        var task = new TaskNode
        {
            Id = "task1",
            Description = "Test",
            ExpectedTypes = new List<string> { "Result" }
        };

        // Act
        var context = builder.BuildCodeGenerationContext(task, new Dictionary<string, TaskNode>());

        // Assert
        Assert.Contains("AMBIGUITY", context);
        Assert.Contains("Result", context);
    }

    [Fact]
    public void BuildInterfaceImplementationContext_ListsRequiredMethods()
    {
        // Arrange
        var catalog = new ContractCatalog();
        catalog.Interfaces.Add(new InterfaceContract
        {
            Name = "IDataProcessor",
            Namespace = "TestProject.Services",
            Methods = new List<MethodSignatureContract>
            {
                new()
                {
                    Name = "ProcessAsync",
                    ReturnType = "Task<ProcessResult>",
                    Parameters = new List<ParameterContract>
                    {
                        new() { Name = "input", Type = "InputData" },
                        new() { Name = "token", Type = "CancellationToken" }
                    }
                }
            }
        });
        catalog.Freeze();
        
        var builder = new PromptContextBuilder(catalog, new SymbolTable(), new TypeRegistry());
        var task = new TaskNode { Id = "task1" };

        // Act
        var context = builder.BuildInterfaceImplementationContext("IDataProcessor", task);

        // Assert
        Assert.Contains("INTERFACE IMPLEMENTATION REQUIREMENTS", context);
        Assert.Contains("Task<ProcessResult> ProcessAsync", context);
        Assert.Contains("InputData input", context);
        Assert.Contains("CancellationToken token", context);
    }

    [Fact]
    public void BuildAbstractClassImplementationContext_WarnsAboutSealedClass()
    {
        // Arrange
        var catalog = new ContractCatalog();
        catalog.AbstractClasses.Add(new AbstractClassContract
        {
            Name = "SealedBase",
            Namespace = "TestProject.Services",
            IsSealed = true
        });
        catalog.Freeze();
        
        var builder = new PromptContextBuilder(catalog, new SymbolTable(), new TypeRegistry());
        var task = new TaskNode { Id = "task1" };

        // Act
        var context = builder.BuildAbstractClassImplementationContext("SealedBase", task);

        // Assert
        Assert.Contains("SEALED", context);
        Assert.Contains("DO NOT inherit", context);
        Assert.Contains("composition", context);
    }

    [Fact]
    public void BuildEnumUsageContext_ListsValidMembers()
    {
        // Arrange
        var catalog = new ContractCatalog();
        catalog.Enums.Add(new EnumContract
        {
            Name = "Priority",
            Namespace = "TestProject.Models",
            Members = new List<EnumMemberContract>
            {
                new() { Name = "Low" },
                new() { Name = "Medium" },
                new() { Name = "High" }
            }
        });
        catalog.Freeze();
        
        var builder = new PromptContextBuilder(catalog, new SymbolTable(), new TypeRegistry());

        // Act
        var context = builder.BuildEnumUsageContext("Priority");

        // Assert
        Assert.Contains("VALID ENUM MEMBERS", context);
        Assert.Contains("Priority.Low", context);
        Assert.Contains("Priority.Medium", context);
        Assert.Contains("Priority.High", context);
    }

    [Fact]
    public void ValidateAgainstContracts_DetectsRedefinedTypes()
    {
        // Arrange
        var catalog = new ContractCatalog();
        catalog.Interfaces.Add(new InterfaceContract
        {
            Name = "IDataService",
            Namespace = "TestProject.Services"
        });
        catalog.Freeze();
        
        var builder = new PromptContextBuilder(catalog, new SymbolTable(), new TypeRegistry());
        
        var task = new TaskNode
        {
            Id = "task1",
            ExpectedTypes = new List<string> { "DataServiceImpl" } // Not IDataService
        };

        var generatedCode = @"
namespace TestProject.Services;

public interface IDataService // REDEFINITION - should be flagged
{
    void DoSomething();
}

public class DataServiceImpl : IDataService
{
    public void DoSomething() { }
}";

        // Act
        var errors = builder.ValidateAgainstContracts(generatedCode, task);

        // Assert
        Assert.Single(errors);
        Assert.Contains("IDataService", errors[0]);
        Assert.Contains("redefines", errors[0].ToLower());
    }
}

/// <summary>
/// Tests for AtomCompilationService.
/// </summary>
public class AtomCompilationServiceTests
{
    [Fact]
    public void CompileAtom_ReturnsSuccess_ForValidCode()
    {
        // Arrange
        var assemblyManager = new AssemblyReferenceManager(null);
        var service = new AtomCompilationService(assemblyManager);
        
        var validCode = @"
using System;

namespace TestProject;

public class SimpleClass
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
}";

        // Act
        var result = service.CompileAtom(validCode);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.ClassifiedDiagnostics);
    }

    [Fact]
    public void CompileAtom_DetectsSyntaxErrors()
    {
        // Arrange
        var assemblyManager = new AssemblyReferenceManager(null);
        var service = new AtomCompilationService(assemblyManager);
        
        var invalidCode = @"
using System;

namespace TestProject

public class BrokenClass
{
    public int Value { get; set; }
}";

        // Act
        var result = service.CompileAtom(invalidCode);

        // Assert
        Assert.False(result.Success);
        Assert.NotEmpty(result.ClassifiedDiagnostics);
    }

    [Fact]
    public void CompileAtom_WithContractCode_ResolvesReferences()
    {
        // Arrange
        var assemblyManager = new AssemblyReferenceManager(null);
        var service = new AtomCompilationService(assemblyManager);
        
        var contractCode = @"
namespace TestProject.Contracts;

public interface IDataService
{
    string GetData();
}";

        var implementationCode = @"
using TestProject.Contracts;

namespace TestProject.Services;

public class DataService : IDataService
{
    public string GetData() => ""test"";
}";

        // Act
        var result = service.CompileAtom(implementationCode, contractCode);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateAgainstContracts_DetectsDuplicateTypes()
    {
        // Arrange
        var assemblyManager = new AssemblyReferenceManager(null);
        var catalog = new ContractCatalog();
        catalog.Models.Add(new ModelContract
        {
            Name = "UserInfo",
            Namespace = "TestProject.Models"
        });
        catalog.Freeze();
        
        var service = new AtomCompilationService(assemblyManager, catalog);
        
        var code = @"
namespace TestProject.Services;

// This redefines UserInfo which exists in TestProject.Models
public class UserInfo
{
    public int Id { get; set; }
}";

        // Act
        var violations = service.ValidateAgainstContracts(code);

        // Assert
        Assert.Single(violations);
        Assert.Equal(ContractViolationType.DuplicateType, violations[0].ViolationType);
    }

    [Fact]
    public void ValidateAgainstContracts_DetectsSealedInheritance()
    {
        // Arrange
        var assemblyManager = new AssemblyReferenceManager(null);
        var catalog = new ContractCatalog();
        catalog.AbstractClasses.Add(new AbstractClassContract
        {
            Name = "SealedResponse",
            Namespace = "TestProject.Models",
            IsSealed = true
        });
        catalog.Freeze();
        
        var service = new AtomCompilationService(assemblyManager, catalog);
        
        var code = @"
using TestProject.Models;

namespace TestProject.Services;

// This tries to inherit from a sealed class
public class ExtendedResponse : SealedResponse
{
    public string Extra { get; set; }
}";

        // Act
        var violations = service.ValidateAgainstContracts(code);

        // Assert
        Assert.Single(violations);
        Assert.Equal(ContractViolationType.SealedInheritance, violations[0].ViolationType);
    }

    [Fact]
    public void ValidateAgainstContracts_DetectsInvalidEnumMembers()
    {
        // Arrange
        var assemblyManager = new AssemblyReferenceManager(null);
        var catalog = new ContractCatalog();
        catalog.Enums.Add(new EnumContract
        {
            Name = "Status",
            Namespace = "TestProject.Models",
            Members = new List<EnumMemberContract>
            {
                new() { Name = "Active" },
                new() { Name = "Inactive" }
            }
        });
        catalog.Freeze();
        
        var service = new AtomCompilationService(assemblyManager, catalog);
        
        var code = @"
namespace TestProject.Services;

public class Handler
{
    public void Process()
    {
        var status = Status.Unknown; // Unknown doesn't exist!
    }
}";

        // Act
        var violations = service.ValidateAgainstContracts(code);

        // Assert
        Assert.Single(violations);
        Assert.Equal(ContractViolationType.InvalidEnumMember, violations[0].ViolationType);
    }

    [Fact]
    public void GenerateCompilationSummary_ProducesReadableSummary()
    {
        // Arrange
        var assemblyManager = new AssemblyReferenceManager(null);
        var service = new AtomCompilationService(assemblyManager);
        
        var result = new AtomCompilationResult
        {
            Success = false,
            ClassifiedDiagnostics = new List<ClassifiedDiagnostic>
            {
                new()
                {
                    Category = AtomCompilationService.DiagnosticCategory.MissingInterfaceMember,
                    Message = "'MyClass' does not implement interface member 'IMyInterface.DoWork()'",
                    IsAutoFixable = true,
                    SuggestedFix = "Add implementation for: IMyInterface.DoWork()"
                },
                new()
                {
                    Category = AtomCompilationService.DiagnosticCategory.AmbiguousReference,
                    Message = "'Result' is an ambiguous reference",
                    IsAutoFixable = true,
                    SuggestedFix = "Use fully qualified type name"
                }
            }
        };

        // Act
        var summary = service.GenerateCompilationSummary(result);

        // Assert
        Assert.Contains("COMPILATION ERRORS", summary);
        Assert.Contains("MissingInterfaceMember", summary);
        Assert.Contains("AmbiguousReference", summary);
        Assert.Contains("IMyInterface.DoWork()", summary);
    }

    [Fact]
    public void AtomCompilationResult_GetByCategory_FiltersCorrectly()
    {
        // Arrange
        var result = new AtomCompilationResult
        {
            ClassifiedDiagnostics = new List<ClassifiedDiagnostic>
            {
                new() { Category = AtomCompilationService.DiagnosticCategory.MissingUsing },
                new() { Category = AtomCompilationService.DiagnosticCategory.MissingUsing },
                new() { Category = AtomCompilationService.DiagnosticCategory.SymbolCollision },
                new() { Category = AtomCompilationService.DiagnosticCategory.SignatureMismatch }
            }
        };

        // Act
        var missingUsings = result.GetByCategory(AtomCompilationService.DiagnosticCategory.MissingUsing).ToList();
        var collisions = result.GetByCategory(AtomCompilationService.DiagnosticCategory.SymbolCollision).ToList();

        // Assert
        Assert.Equal(2, missingUsings.Count);
        Assert.Single(collisions);
    }

    [Fact]
    public void AtomCompilationResult_GetAutoFixable_ReturnsOnlyFixable()
    {
        // Arrange
        var result = new AtomCompilationResult
        {
            ClassifiedDiagnostics = new List<ClassifiedDiagnostic>
            {
                new() { Category = AtomCompilationService.DiagnosticCategory.MissingUsing, IsAutoFixable = true },
                new() { Category = AtomCompilationService.DiagnosticCategory.Other, IsAutoFixable = false },
                new() { Category = AtomCompilationService.DiagnosticCategory.AmbiguousReference, IsAutoFixable = true }
            }
        };

        // Act
        var fixable = result.GetAutoFixable().ToList();

        // Assert
        Assert.Equal(2, fixable.Count);
        Assert.All(fixable, d => Assert.True(d.IsAutoFixable));
    }
}
