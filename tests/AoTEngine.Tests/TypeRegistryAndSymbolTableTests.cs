using AoTEngine.Models;
using Xunit;

namespace AoTEngine.Tests;

public class TypeRegistryTests
{
    [Fact]
    public void TryRegister_NewType_ShouldSucceed()
    {
        // Arrange
        var registry = new TypeRegistry();
        var entry = new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class,
            OwnerTaskId = "task1"
        };

        // Act
        var result = registry.TryRegister(entry);

        // Assert
        Assert.True(result);
        Assert.True(registry.Contains("MyNamespace.MyClass"));
        Assert.Empty(registry.Conflicts);
    }

    [Fact]
    public void TryRegister_DuplicateType_ShouldDetectConflict()
    {
        // Arrange
        var registry = new TypeRegistry();
        var entry1 = new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class,
            OwnerTaskId = "task1"
        };
        var entry2 = new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class,
            OwnerTaskId = "task2"
        };

        // Act
        registry.TryRegister(entry1);
        var result = registry.TryRegister(entry2);

        // Assert
        Assert.False(result);
        Assert.Single(registry.Conflicts);
        Assert.Equal("MyNamespace.MyClass", registry.Conflicts[0].FullyQualifiedName);
    }

    [Fact]
    public void TryRegister_DuplicateInterface_ShouldSuggestKeepFirst()
    {
        // Arrange
        var registry = new TypeRegistry();
        var entry1 = new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.IValidator",
            Namespace = "MyNamespace",
            TypeName = "IValidator",
            Kind = ProjectTypeKind.Interface,
            OwnerTaskId = "task1"
        };
        var entry2 = new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.IValidator",
            Namespace = "MyNamespace",
            TypeName = "IValidator",
            Kind = ProjectTypeKind.Interface,
            OwnerTaskId = "task2"
        };

        // Act
        registry.TryRegister(entry1);
        registry.TryRegister(entry2);

        // Assert
        Assert.Single(registry.Conflicts);
        Assert.Equal(ConflictResolution.KeepFirst, registry.Conflicts[0].SuggestedResolution);
    }

    [Fact]
    public void TryRegister_DuplicateClassWithoutConflictingMembers_ShouldSuggestMergeAsPartial()
    {
        // Arrange
        var registry = new TypeRegistry();
        var entry1 = new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class,
            OwnerTaskId = "task1",
            Members = new List<MemberSignature>
            {
                new MemberSignature { Name = "Method1", Kind = ProjectMemberKind.Method }
            }
        };
        var entry2 = new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class,
            OwnerTaskId = "task2",
            Members = new List<MemberSignature>
            {
                new MemberSignature { Name = "Method2", Kind = ProjectMemberKind.Method }
            }
        };

        // Act
        registry.TryRegister(entry1);
        registry.TryRegister(entry2);

        // Assert
        Assert.Single(registry.Conflicts);
        Assert.Equal(ConflictResolution.MergeAsPartial, registry.Conflicts[0].SuggestedResolution);
    }

    [Fact]
    public void TryRegister_DuplicateClassWithConflictingMembers_ShouldSuggestRemoveDuplicate()
    {
        // Arrange
        var registry = new TypeRegistry();
        var entry1 = new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class,
            OwnerTaskId = "task1",
            Members = new List<MemberSignature>
            {
                new MemberSignature 
                { 
                    Name = "MyClass", 
                    Kind = ProjectMemberKind.Constructor,
                    ParameterTypes = new List<string> { "string" }
                }
            }
        };
        var entry2 = new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class,
            OwnerTaskId = "task2",
            Members = new List<MemberSignature>
            {
                new MemberSignature 
                { 
                    Name = "MyClass", 
                    Kind = ProjectMemberKind.Constructor,
                    ParameterTypes = new List<string> { "string" }
                }
            }
        };

        // Act
        registry.TryRegister(entry1);
        registry.TryRegister(entry2);

        // Assert
        Assert.Single(registry.Conflicts);
        Assert.Equal(ConflictResolution.RemoveDuplicate, registry.Conflicts[0].SuggestedResolution);
        Assert.Single(registry.Conflicts[0].ConflictingMembers);
    }

    [Fact]
    public void GetTypesBySimpleName_ShouldReturnAllTypesWithSameName()
    {
        // Arrange
        var registry = new TypeRegistry();
        registry.TryRegister(new TypeRegistryEntry
        {
            FullyQualifiedName = "Namespace1.MyClass",
            Namespace = "Namespace1",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class
        });
        registry.TryRegister(new TypeRegistryEntry
        {
            FullyQualifiedName = "Namespace2.MyClass",
            Namespace = "Namespace2",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class
        });

        // Act
        var types = registry.GetTypesBySimpleName("MyClass");

        // Assert
        Assert.Equal(2, types.Count);
    }

    [Fact]
    public void IsAmbiguous_ShouldReturnTrueWhenMultipleTypesWithSameName()
    {
        // Arrange
        var registry = new TypeRegistry();
        registry.TryRegister(new TypeRegistryEntry
        {
            FullyQualifiedName = "Namespace1.MyClass",
            Namespace = "Namespace1",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class
        });
        registry.TryRegister(new TypeRegistryEntry
        {
            FullyQualifiedName = "Namespace2.MyClass",
            Namespace = "Namespace2",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class
        });

        // Act & Assert
        Assert.True(registry.IsAmbiguous("MyClass"));
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        var registry = new TypeRegistry();
        registry.TryRegister(new TypeRegistryEntry
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            TypeName = "MyClass",
            Kind = ProjectTypeKind.Class
        });

        // Act
        registry.Clear();

        // Assert
        Assert.Empty(registry.Types);
        Assert.Empty(registry.Conflicts);
    }
}

public class SymbolTableTests
{
    [Fact]
    public void TryRegister_NewSymbol_ShouldSucceed()
    {
        // Arrange
        var table = new SymbolTable();
        var symbol = new ProjectSymbolInfo
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            Name = "MyClass",
            Kind = ProjectSymbolKind.Type,
            DefinedByTaskId = "task1"
        };

        // Act
        var result = table.TryRegister(symbol);

        // Assert
        Assert.True(result);
        Assert.True(table.Contains("MyNamespace.MyClass"));
    }

    [Fact]
    public void TryRegister_DuplicateSymbol_ShouldReturnFalse()
    {
        // Arrange
        var table = new SymbolTable();
        var symbol1 = new ProjectSymbolInfo
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            Name = "MyClass",
            Kind = ProjectSymbolKind.Type
        };
        var symbol2 = new ProjectSymbolInfo
        {
            FullyQualifiedName = "MyNamespace.MyClass",
            Namespace = "MyNamespace",
            Name = "MyClass",
            Kind = ProjectSymbolKind.Type
        };

        // Act
        table.TryRegister(symbol1);
        var result = table.TryRegister(symbol2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetSymbolsInNamespace_ShouldReturnCorrectSymbols()
    {
        // Arrange
        var table = new SymbolTable();
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "MyNamespace.Class1",
            Namespace = "MyNamespace",
            Name = "Class1",
            Kind = ProjectSymbolKind.Type
        });
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "MyNamespace.Class2",
            Namespace = "MyNamespace",
            Name = "Class2",
            Kind = ProjectSymbolKind.Type
        });
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "OtherNamespace.Class3",
            Namespace = "OtherNamespace",
            Name = "Class3",
            Kind = ProjectSymbolKind.Type
        });

        // Act
        var symbols = table.GetSymbolsInNamespace("MyNamespace").ToList();

        // Assert
        Assert.Equal(2, symbols.Count);
        Assert.All(symbols, s => Assert.Equal("MyNamespace", s.Namespace));
    }

    [Fact]
    public void GetSymbolsByTask_ShouldReturnCorrectSymbols()
    {
        // Arrange
        var table = new SymbolTable();
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "Ns.Class1",
            Namespace = "Ns",
            Name = "Class1",
            Kind = ProjectSymbolKind.Type,
            DefinedByTaskId = "task1"
        });
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "Ns.Class2",
            Namespace = "Ns",
            Name = "Class2",
            Kind = ProjectSymbolKind.Type,
            DefinedByTaskId = "task1"
        });
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "Ns.Class3",
            Namespace = "Ns",
            Name = "Class3",
            Kind = ProjectSymbolKind.Type,
            DefinedByTaskId = "task2"
        });

        // Act
        var symbols = table.GetSymbolsByTask("task1").ToList();

        // Assert
        Assert.Equal(2, symbols.Count);
        Assert.All(symbols, s => Assert.Equal("task1", s.DefinedByTaskId));
    }

    [Fact]
    public void GenerateKnownTypesBlock_ShouldGenerateCorrectFormat()
    {
        // Arrange
        var table = new SymbolTable();
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "MyNamespace.IValidator",
            Namespace = "MyNamespace",
            Name = "IValidator",
            Kind = ProjectSymbolKind.Interface,
            Signature = "public interface IValidator"
        });
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "MyNamespace.MyEnum",
            Namespace = "MyNamespace",
            Name = "MyEnum",
            Kind = ProjectSymbolKind.Enum
        });

        // Act
        var block = table.GenerateKnownTypesBlock();

        // Assert
        Assert.Contains("Existing types (DO NOT redefine", block);
        Assert.Contains("MyNamespace.IValidator", block);
        Assert.Contains("MyNamespace.MyEnum", block);
        Assert.Contains("interface", block);
        Assert.Contains("enum", block);
    }

    [Fact]
    public void GenerateMetadata_ShouldReturnCorrectMetadata()
    {
        // Arrange
        var table = new SymbolTable();
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "Ns.Class1",
            Namespace = "Ns",
            Name = "Class1",
            Kind = ProjectSymbolKind.Type,
            RequiredUsings = new List<string> { "System", "System.Linq" }
        });
        table.TryRegister(new ProjectSymbolInfo
        {
            FullyQualifiedName = "Ns.IService",
            Namespace = "Ns",
            Name = "IService",
            Kind = ProjectSymbolKind.Interface,
            RequiredUsings = new List<string> { "System" }
        });

        // Act
        var metadata = table.GenerateMetadata();

        // Assert
        Assert.Equal(2, metadata.DefinedTypes.Count);
        Assert.Contains("Ns.Class1", metadata.DefinedTypes);
        Assert.Contains("Ns.IService", metadata.DefinedTypes);
        Assert.Equal(2, metadata.RequiredUsings.Count); // Distinct usings
    }
}
