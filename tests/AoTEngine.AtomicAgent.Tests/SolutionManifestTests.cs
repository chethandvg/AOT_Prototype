using AoTEngine.AtomicAgent.Models;
using Xunit;

namespace AoTEngine.AtomicAgent.Tests;

public class SolutionManifestTests
{
    [Fact]
    public void SolutionManifest_DefaultsAreInitialized()
    {
        // Arrange & Act
        var manifest = new SolutionManifest();

        // Assert
        Assert.NotNull(manifest.ProjectMetadata);
        Assert.NotNull(manifest.ProjectHierarchy);
        Assert.NotNull(manifest.SemanticSymbolTable);
        Assert.NotNull(manifest.Atoms);
        Assert.Equal("AtomicAgentPrototype", manifest.ProjectMetadata.Name);
        Assert.Equal("AtomicAgent", manifest.ProjectMetadata.RootNamespace);
        Assert.Equal("net9.0", manifest.ProjectMetadata.TargetFramework);
    }

    [Fact]
    public void Atom_StatusConstants_AreCorrect()
    {
        // Assert
        Assert.Equal("pending", AtomStatus.Pending);
        Assert.Equal("in_progress", AtomStatus.InProgress);
        Assert.Equal("review", AtomStatus.Review);
        Assert.Equal("completed", AtomStatus.Completed);
        Assert.Equal("failed", AtomStatus.Failed);
    }

    [Fact]
    public void Atom_TypeConstants_AreCorrect()
    {
        // Assert
        Assert.Equal("interface", AtomType.Interface);
        Assert.Equal("dto", AtomType.Dto);
        Assert.Equal("implementation", AtomType.Implementation);
        Assert.Equal("test", AtomType.Test);
    }

    [Fact]
    public void Layer_DefaultsAreInitialized()
    {
        // Arrange & Act
        var layer = new Layer();

        // Assert
        Assert.NotNull(layer.Description);
        Assert.NotNull(layer.ProjectPath);
        Assert.NotNull(layer.AllowedDependencies);
        Assert.Empty(layer.AllowedDependencies);
    }

    [Fact]
    public void InterfaceSignature_CanStoreMethodSignatures()
    {
        // Arrange
        var signature = new InterfaceSignature
        {
            Name = "IUserRepository",
            Namespace = "MyApp.Core",
            Methods = new List<string>
            {
                "Task<User> GetByIdAsync(int id)",
                "Task SaveAsync(User user)"
            }
        };

        // Assert
        Assert.Equal("IUserRepository", signature.Name);
        Assert.Equal("MyApp.Core", signature.Namespace);
        Assert.Equal(2, signature.Methods.Count);
        Assert.Contains("Task<User> GetByIdAsync(int id)", signature.Methods);
    }

    [Fact]
    public void DtoSignature_CanStorePropertySignatures()
    {
        // Arrange
        var signature = new DtoSignature
        {
            Name = "UserDto",
            Namespace = "MyApp.Core",
            Properties = new List<string>
            {
                "public int Id { get; set; }",
                "public string Name { get; set; }"
            }
        };

        // Assert
        Assert.Equal("UserDto", signature.Name);
        Assert.Equal("MyApp.Core", signature.Namespace);
        Assert.Equal(2, signature.Properties.Count);
    }
}
