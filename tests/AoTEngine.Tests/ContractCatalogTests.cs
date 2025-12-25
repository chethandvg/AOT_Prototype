using AoTEngine.Models;
using AoTEngine.Services;
using System.Linq;
using Xunit;

namespace AoTEngine.Tests;

/// <summary>
/// Tests for ContractCatalog and related contract-first generation functionality.
/// </summary>
public class ContractCatalogTests
{
    [Fact]
    public void ContractCatalog_ContainsType_ReturnsTrue_ForExistingEnum()
    {
        // Arrange
        var catalog = new ContractCatalog
        {
            ProjectName = "TestProject",
            RootNamespace = "TestProject"
        };
        
        catalog.Enums.Add(new EnumContract
        {
            Name = "StatusType",
            Namespace = "TestProject.Models",
            Members = new List<EnumMemberContract>
            {
                new() { Name = "Active" },
                new() { Name = "Inactive" }
            }
        });

        // Act & Assert
        Assert.True(catalog.ContainsType("StatusType"));
        Assert.True(catalog.ContainsType("TestProject.Models.StatusType"));
        Assert.False(catalog.ContainsType("NonExistent"));
    }

    [Fact]
    public void ContractCatalog_GetContract_ReturnsCorrectContract()
    {
        // Arrange
        var catalog = new ContractCatalog
        {
            ProjectName = "TestProject"
        };
        
        var interfaceContract = new InterfaceContract
        {
            Name = "IDataService",
            Namespace = "TestProject.Services"
        };
        catalog.Interfaces.Add(interfaceContract);

        // Act
        var result = catalog.GetContract("IDataService");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("IDataService", result.Name);
        Assert.Equal("TestProject.Services", result.Namespace);
    }

    [Fact]
    public void ContractCatalog_Freeze_SetsIsFrozenAndTimestamp()
    {
        // Arrange
        var catalog = new ContractCatalog();
        Assert.False(catalog.IsFrozen);
        Assert.Null(catalog.FrozenAtUtc);

        // Act
        catalog.Freeze();

        // Assert
        Assert.True(catalog.IsFrozen);
        Assert.NotNull(catalog.FrozenAtUtc);
        Assert.True(catalog.FrozenAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void ContractCatalog_GetAllContracts_ReturnsAllTypes()
    {
        // Arrange
        var catalog = new ContractCatalog();
        
        catalog.Enums.Add(new EnumContract { Name = "Enum1", Namespace = "Test" });
        catalog.Interfaces.Add(new InterfaceContract { Name = "IInterface1", Namespace = "Test" });
        catalog.Models.Add(new ModelContract { Name = "Model1", Namespace = "Test" });
        catalog.AbstractClasses.Add(new AbstractClassContract { Name = "AbstractBase", Namespace = "Test" });

        // Act
        var all = catalog.GetAllContracts().ToList();

        // Assert
        Assert.Equal(4, all.Count);
        Assert.Contains(all, c => c.Name == "Enum1");
        Assert.Contains(all, c => c.Name == "IInterface1");
        Assert.Contains(all, c => c.Name == "Model1");
        Assert.Contains(all, c => c.Name == "AbstractBase");
    }

    [Fact]
    public void EnumContract_GenerateCode_ProducesValidEnumDefinition()
    {
        // Arrange
        var enumContract = new EnumContract
        {
            Name = "StatusType",
            Namespace = "TestProject.Models",
            AccessModifier = "public",
            Documentation = "Status values for entities",
            Members = new List<EnumMemberContract>
            {
                new() { Name = "Active", Value = 0 },
                new() { Name = "Inactive", Value = 1 },
                new() { Name = "Pending", Value = 2 }
            }
        };

        // Act
        var code = enumContract.GenerateCode();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("Status values for entities", code);
        Assert.Contains("public enum StatusType", code);
        Assert.Contains("Active = 0", code);
        Assert.Contains("Inactive = 1", code);
        Assert.Contains("Pending = 2", code);
    }

    [Fact]
    public void EnumContract_GenerateCode_WithFlags_IncludesFlagsAttribute()
    {
        // Arrange
        var enumContract = new EnumContract
        {
            Name = "PermissionFlags",
            Namespace = "TestProject.Models",
            IsFlags = true,
            Members = new List<EnumMemberContract>
            {
                new() { Name = "Read", Value = 1 },
                new() { Name = "Write", Value = 2 },
                new() { Name = "Execute", Value = 4 }
            }
        };

        // Act
        var code = enumContract.GenerateCode();

        // Assert
        Assert.Contains("[Flags]", code);
        Assert.Contains("public enum PermissionFlags", code);
    }

    [Fact]
    public void InterfaceContract_GenerateCode_ProducesValidInterfaceDefinition()
    {
        // Arrange
        var interfaceContract = new InterfaceContract
        {
            Name = "IDataService",
            Namespace = "TestProject.Services",
            Documentation = "Service interface for data operations",
            Methods = new List<MethodSignatureContract>
            {
                new()
                {
                    Name = "GetDataAsync",
                    ReturnType = "Task<Data>",
                    Parameters = new List<ParameterContract>
                    {
                        new() { Name = "id", Type = "int" },
                        new() { Name = "cancellationToken", Type = "CancellationToken" }
                    }
                }
            },
            Properties = new List<PropertySignatureContract>
            {
                new() { Name = "IsConnected", Type = "bool", HasGetter = true, HasSetter = false }
            }
        };

        // Act
        var code = interfaceContract.GenerateCode();

        // Assert
        Assert.Contains("public interface IDataService", code);
        Assert.Contains("Task<Data> GetDataAsync(int id, CancellationToken cancellationToken);", code);
        Assert.Contains("bool IsConnected { get; }", code);
    }

    [Fact]
    public void InterfaceContract_GenerateCode_WithBaseInterfaces()
    {
        // Arrange
        var interfaceContract = new InterfaceContract
        {
            Name = "ISpecialService",
            Namespace = "TestProject.Services",
            BaseInterfaces = new List<string> { "IDisposable", "IAsyncDisposable" }
        };

        // Act
        var code = interfaceContract.GenerateCode();

        // Assert
        Assert.Contains("public interface ISpecialService : IDisposable, IAsyncDisposable", code);
    }

    [Fact]
    public void ModelContract_GenerateCode_ProducesValidClassDefinition()
    {
        // Arrange
        var modelContract = new ModelContract
        {
            Name = "UserInfo",
            Namespace = "TestProject.Models",
            Documentation = "User information DTO",
            Properties = new List<PropertySignatureContract>
            {
                new() { Name = "Id", Type = "int", HasGetter = true, HasSetter = true },
                new() { Name = "Name", Type = "string", HasGetter = true, HasSetter = true },
                new() { Name = "Email", Type = "string?", HasGetter = true, HasSetter = true }
            }
        };

        // Act
        var code = modelContract.GenerateCode();

        // Assert
        Assert.Contains("public class UserInfo", code);
        Assert.Contains("public int Id { get; set; }", code);
        Assert.Contains("public string Name { get; set; }", code);
        Assert.Contains("public string? Email { get; set; }", code);
    }

    [Fact]
    public void ModelContract_GenerateCode_AsRecord()
    {
        // Arrange
        var modelContract = new ModelContract
        {
            Name = "DataRecord",
            Namespace = "TestProject.Models",
            IsRecord = true,
            Properties = new List<PropertySignatureContract>
            {
                new() { Name = "Value", Type = "string", HasGetter = true, HasSetter = true }
            }
        };

        // Act
        var code = modelContract.GenerateCode();

        // Assert
        Assert.Contains("public record DataRecord", code);
    }

    [Fact]
    public void AbstractClassContract_GenerateCode_ProducesValidAbstractClass()
    {
        // Arrange
        var abstractContract = new AbstractClassContract
        {
            Name = "BaseReportExporter",
            Namespace = "TestProject.Services",
            Documentation = "Base class for report exporters",
            AbstractMethods = new List<MethodSignatureContract>
            {
                new()
                {
                    Name = "ExportAsync",
                    ReturnType = "Task",
                    Parameters = new List<ParameterContract>
                    {
                        new() { Name = "sections", Type = "IReadOnlyList<ReportSection>" },
                        new() { Name = "options", Type = "ExportOptions" },
                        new() { Name = "cancellationToken", Type = "CancellationToken" }
                    }
                }
            }
        };

        // Act
        var code = abstractContract.GenerateCode();

        // Assert
        Assert.Contains("public abstract class BaseReportExporter", code);
        Assert.Contains("public abstract Task ExportAsync(IReadOnlyList<ReportSection> sections, ExportOptions options, CancellationToken cancellationToken);", code);
    }

    [Fact]
    public void AbstractClassContract_GenerateCode_WhenSealed_GeneratesSealedClass()
    {
        // Arrange - A class cannot be both sealed and abstract in C#
        // When IsSealed is true, it generates a sealed class (not abstract)
        var contract = new AbstractClassContract
        {
            Name = "FinalResponse",
            Namespace = "TestProject.Models",
            IsSealed = true
        };

        // Act
        var code = contract.GenerateCode();

        // Assert - should be sealed class, not sealed abstract class
        Assert.Contains("public sealed class FinalResponse", code);
        Assert.DoesNotContain("abstract", code);
    }
}

/// <summary>
/// Tests for the enhanced SymbolTable with collision detection.
/// </summary>
public class EnhancedSymbolTableTests
{
    [Fact]
    public void SymbolTable_DetectsAmbiguousNames()
    {
        // Arrange
        var symbolTable = new SymbolTable();
        
        symbolTable.TryRegister(new ProjectSymbolInfo
        {
            Name = "Config", // Use a name that doesn't trigger model detection
            Namespace = "TestProject.Core",
            FullyQualifiedName = "TestProject.Core.Config",
            Kind = ProjectSymbolKind.Type
        });
        
        symbolTable.TryRegister(new ProjectSymbolInfo
        {
            Name = "Config",
            Namespace = "TestProject.Settings",
            FullyQualifiedName = "TestProject.Settings.Config",
            Kind = ProjectSymbolKind.Type
        });

        // Act
        var isAmbiguous = symbolTable.IsAmbiguous("Config");
        var symbols = symbolTable.GetSymbolsBySimpleName("Config").ToList();

        // Assert
        Assert.True(isAmbiguous);
        Assert.Equal(2, symbols.Count);
        Assert.Single(symbolTable.Collisions);
        Assert.Equal(SymbolCollisionType.AmbiguousName, symbolTable.Collisions[0].CollisionType);
    }

    [Fact]
    public void SymbolTable_DetectsMisplacedModels()
    {
        // Arrange
        var symbolTable = new SymbolTable();
        
        // First, add a symbol in the correct namespace
        symbolTable.TryRegister(new ProjectSymbolInfo
        {
            Name = "UserInfo",
            Namespace = "TestProject.Models",
            FullyQualifiedName = "TestProject.Models.UserInfo",
            Kind = ProjectSymbolKind.Type
        });
        
        // Add same name in Services namespace (incorrect for a model)
        symbolTable.TryRegister(new ProjectSymbolInfo
        {
            Name = "UserInfo",
            Namespace = "TestProject.Services",
            FullyQualifiedName = "TestProject.Services.UserInfo",
            Kind = ProjectSymbolKind.Type
        });

        // Assert
        Assert.Single(symbolTable.Collisions);
        // The collision should indicate misplaced model
        var collision = symbolTable.Collisions[0];
        Assert.Equal("UserInfo", collision.SimpleName);
    }

    [Fact]
    public void SymbolTable_ValidateNamespaceConventions_DetectsModelsInServices()
    {
        // Arrange
        var symbolTable = new SymbolTable();
        var symbol = new ProjectSymbolInfo
        {
            Name = "CustomerInfo",
            Namespace = "TestProject.Services",
            FullyQualifiedName = "TestProject.Services.CustomerInfo",
            Kind = ProjectSymbolKind.Type
        };

        // Act
        var errors = symbolTable.ValidateNamespaceConventions(symbol);

        // Assert
        Assert.Single(errors);
        Assert.Contains(".Models", errors[0]);
    }

    [Fact]
    public void SymbolTable_GetSuggestedAlias_ReturnsPreferredNamespace()
    {
        // Arrange
        var symbolTable = new SymbolTable();
        
        symbolTable.TryRegister(new ProjectSymbolInfo
        {
            Name = "Result",
            Namespace = "TestProject.Services",
            FullyQualifiedName = "TestProject.Services.Result",
            Kind = ProjectSymbolKind.Type
        });
        
        symbolTable.TryRegister(new ProjectSymbolInfo
        {
            Name = "Result",
            Namespace = "TestProject.Models",
            FullyQualifiedName = "TestProject.Models.Result",
            Kind = ProjectSymbolKind.Type
        });

        // Act - when specifying Services as preferred, it should return the Services one
        var suggested = symbolTable.GetSuggestedAlias("Result", "TestProject.Services");

        // Assert - should return the preferred namespace's version
        Assert.Equal("TestProject.Services.Result", suggested);
    }

    [Fact]
    public void SymbolTable_GenerateUsingAliases_CreatesAliasesForAmbiguousTypes()
    {
        // Arrange
        var symbolTable = new SymbolTable();
        
        symbolTable.TryRegister(new ProjectSymbolInfo
        {
            Name = "Config",
            Namespace = "TestProject.Core",
            FullyQualifiedName = "TestProject.Core.Config",
            Kind = ProjectSymbolKind.Type
        });
        
        symbolTable.TryRegister(new ProjectSymbolInfo
        {
            Name = "Config",
            Namespace = "TestProject.Settings",
            FullyQualifiedName = "TestProject.Settings.Config",
            Kind = ProjectSymbolKind.Type
        });

        // Act
        var aliases = symbolTable.GenerateUsingAliases();

        // Assert
        Assert.Single(aliases);
        Assert.Contains("using", aliases[0]);
        Assert.Contains("TestProject.Settings.Config", aliases[0]);
    }
}

/// <summary>
/// Tests for ContractManifestService.
/// </summary>
public class ContractManifestServiceTests
{
    [Fact]
    public void ValidateEnumMember_ReturnsTrue_ForValidMember()
    {
        // Arrange
        var service = new ContractManifestService();
        var catalog = new ContractCatalog();
        catalog.Enums.Add(new EnumContract
        {
            Name = "StatusType",
            Namespace = "Test",
            Members = new List<EnumMemberContract>
            {
                new() { Name = "Active" },
                new() { Name = "Inactive" }
            }
        });

        // Act & Assert
        Assert.True(service.ValidateEnumMember(catalog, "StatusType", "Active"));
        Assert.True(service.ValidateEnumMember(catalog, "StatusType", "Inactive"));
        Assert.False(service.ValidateEnumMember(catalog, "StatusType", "Unknown"));
    }

    [Fact]
    public void GetEnumMembers_ReturnsAllMembers()
    {
        // Arrange
        var service = new ContractManifestService();
        var catalog = new ContractCatalog();
        catalog.Enums.Add(new EnumContract
        {
            Name = "Priority",
            Namespace = "Test",
            Members = new List<EnumMemberContract>
            {
                new() { Name = "Low" },
                new() { Name = "Medium" },
                new() { Name = "High" }
            }
        });

        // Act
        var members = service.GetEnumMembers(catalog, "Priority");

        // Assert
        Assert.Equal(3, members.Count);
        Assert.Contains("Low", members);
        Assert.Contains("Medium", members);
        Assert.Contains("High", members);
    }

    [Fact]
    public void IsTypeSealed_ReturnsTrue_ForSealedClass()
    {
        // Arrange
        var service = new ContractManifestService();
        var catalog = new ContractCatalog();
        catalog.AbstractClasses.Add(new AbstractClassContract
        {
            Name = "SealedBase",
            Namespace = "Test",
            IsSealed = true
        });
        catalog.AbstractClasses.Add(new AbstractClassContract
        {
            Name = "OpenBase",
            Namespace = "Test",
            IsSealed = false
        });

        // Act & Assert
        Assert.True(service.IsTypeSealed(catalog, "SealedBase"));
        Assert.False(service.IsTypeSealed(catalog, "OpenBase"));
        Assert.False(service.IsTypeSealed(catalog, "NonExistent"));
    }

    [Fact]
    public void ValidateInterfaceMethod_ReturnsValid_ForCorrectSignature()
    {
        // Arrange
        var service = new ContractManifestService();
        var catalog = new ContractCatalog();
        catalog.Interfaces.Add(new InterfaceContract
        {
            Name = "ITestService",
            Namespace = "Test",
            Methods = new List<MethodSignatureContract>
            {
                new()
                {
                    Name = "ProcessAsync",
                    ReturnType = "Task<Result>",
                    Parameters = new List<ParameterContract>
                    {
                        new() { Name = "input", Type = "string" },
                        new() { Name = "token", Type = "CancellationToken" }
                    }
                }
            }
        });

        // Act
        var result = service.ValidateInterfaceMethod(
            catalog,
            "ITestService",
            "ProcessAsync",
            "Task<Result>",
            new List<string> { "string", "CancellationToken" });

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateInterfaceMethod_ReturnsErrors_ForWrongReturnType()
    {
        // Arrange
        var service = new ContractManifestService();
        var catalog = new ContractCatalog();
        catalog.Interfaces.Add(new InterfaceContract
        {
            Name = "ITestService",
            Namespace = "Test",
            Methods = new List<MethodSignatureContract>
            {
                new()
                {
                    Name = "GetValueAsync",
                    ReturnType = "Task<int>",
                    Parameters = new List<ParameterContract>()
                }
            }
        });

        // Act
        var result = service.ValidateInterfaceMethod(
            catalog,
            "ITestService",
            "GetValueAsync",
            "Task<string>", // Wrong return type
            new List<string>());

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("Return type mismatch", result.Errors[0]);
    }
}
