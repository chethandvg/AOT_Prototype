using AoTEngine.AtomicAgent.Models;
using AoTEngine.AtomicAgent.Planner;
using Xunit;

namespace AoTEngine.AtomicAgent.Tests;

public class TopologicalSorterTests
{
    [Fact]
    public void Sort_WithNoDependencies_ReturnsOriginalOrder()
    {
        // Arrange
        var atoms = new List<Atom>
        {
            new Atom { Id = "atom1", Name = "Atom1", Dependencies = new List<string>() },
            new Atom { Id = "atom2", Name = "Atom2", Dependencies = new List<string>() },
            new Atom { Id = "atom3", Name = "Atom3", Dependencies = new List<string>() }
        };

        // Act
        var sorted = TopologicalSorter.Sort(atoms);

        // Assert
        Assert.Equal(3, sorted.Count);
    }

    [Fact]
    public void Sort_WithLinearDependencies_ReturnsCorrectOrder()
    {
        // Arrange
        var atoms = new List<Atom>
        {
            new Atom { Id = "atom3", Name = "Atom3", Dependencies = new List<string> { "atom2" } },
            new Atom { Id = "atom2", Name = "Atom2", Dependencies = new List<string> { "atom1" } },
            new Atom { Id = "atom1", Name = "Atom1", Dependencies = new List<string>() }
        };

        // Act
        var sorted = TopologicalSorter.Sort(atoms);

        // Assert
        Assert.Equal(3, sorted.Count);
        Assert.Equal("atom1", sorted[0].Id);
        Assert.Equal("atom2", sorted[1].Id);
        Assert.Equal("atom3", sorted[2].Id);
    }

    [Fact]
    public void Sort_WithMultipleDependencies_ReturnsValidOrder()
    {
        // Arrange
        var atoms = new List<Atom>
        {
            new Atom { Id = "atom4", Name = "Atom4", Dependencies = new List<string> { "atom2", "atom3" } },
            new Atom { Id = "atom3", Name = "Atom3", Dependencies = new List<string> { "atom1" } },
            new Atom { Id = "atom2", Name = "Atom2", Dependencies = new List<string> { "atom1" } },
            new Atom { Id = "atom1", Name = "Atom1", Dependencies = new List<string>() }
        };

        // Act
        var sorted = TopologicalSorter.Sort(atoms);

        // Assert
        Assert.Equal(4, sorted.Count);
        Assert.Equal("atom1", sorted[0].Id);
        // atom2 and atom3 can be in any order
        Assert.Equal("atom4", sorted[3].Id);
        
        // Verify atom4 comes after its dependencies
        var atom2Index = sorted.FindIndex(a => a.Id == "atom2");
        var atom3Index = sorted.FindIndex(a => a.Id == "atom3");
        var atom4Index = sorted.FindIndex(a => a.Id == "atom4");
        
        Assert.True(atom2Index < atom4Index);
        Assert.True(atom3Index < atom4Index);
    }

    [Fact]
    public void Sort_WithCircularDependency_ThrowsException()
    {
        // Arrange
        var atoms = new List<Atom>
        {
            new Atom { Id = "atom1", Name = "Atom1", Dependencies = new List<string> { "atom2" } },
            new Atom { Id = "atom2", Name = "Atom2", Dependencies = new List<string> { "atom1" } }
        };

        // Act & Assert
        Assert.Throws<CircularDependencyException>(() => TopologicalSorter.Sort(atoms));
    }

    [Fact]
    public void Sort_WithAbstractionsFirst_PlacesInterfacesBeforeImplementations()
    {
        // Arrange
        var atoms = new List<Atom>
        {
            new Atom { Id = "impl1", Name = "UserRepository", Type = AtomType.Implementation, Dependencies = new List<string> { "iface1", "dto1" } },
            new Atom { Id = "iface1", Name = "IUserRepository", Type = AtomType.Interface, Dependencies = new List<string> { "dto1" } },
            new Atom { Id = "dto1", Name = "UserDto", Type = AtomType.Dto, Dependencies = new List<string>() }
        };

        // Act
        var sorted = TopologicalSorter.Sort(atoms);

        // Assert
        Assert.Equal(3, sorted.Count);
        Assert.Equal("dto1", sorted[0].Id);
        Assert.Equal("iface1", sorted[1].Id);
        Assert.Equal("impl1", sorted[2].Id);
    }
}
