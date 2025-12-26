using AoTEngine.Models;
using AoTEngine.Services.Contracts;

namespace AoTEngine.Tests;

public class BranchSynchronizationServiceV2Tests
{
    [Fact]
    public async Task RegisterContract_NewContract_Succeeds()
    {
        // Arrange
        var service = new BranchSynchronizationServiceV2();
        var typeName = "ITestInterface";
        var content = "public interface ITestInterface { void Test(); }";
        var ns = "TestNamespace";

        // Act
        var result = await service.RegisterContractAsync(typeName, content, ns);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.RegisteredContract);
        Assert.Equal(1, result.RegisteredContract.Version);
        Assert.Equal(typeName, result.RegisteredContract.TypeName);
    }

    [Fact]
    public async Task RegisterContract_DuplicateContract_ReturnsSameVersion()
    {
        // Arrange
        var service = new BranchSynchronizationServiceV2();
        var typeName = "ITestInterface";
        var content = "public interface ITestInterface { void Test(); }";
        var ns = "TestNamespace";

        // Act
        var result1 = await service.RegisterContractAsync(typeName, content, ns);
        var result2 = await service.RegisterContractAsync(typeName, content, ns);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Equal(result1.RegisteredContract?.Version, result2.RegisteredContract?.Version);
    }

    [Fact]
    public async Task RegisterContract_ConflictingContract_ReturnsConflict()
    {
        // Arrange
        var service = new BranchSynchronizationServiceV2();
        var typeName = "ITestInterface";
        var content1 = "public interface ITestInterface { void Test(); }";
        var content2 = "public interface ITestInterface { void Different(); }";
        var ns = "TestNamespace";

        // Act
        var result1 = await service.RegisterContractAsync(typeName, content1, ns);
        var result2 = await service.RegisterContractAsync(typeName, content2, ns);

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Equal(ContractConflictType.IncompatibleDefinition, result2.ConflictType);
    }

    [Fact]
    public async Task GetContractSnapshot_ReturnsAllContracts()
    {
        // Arrange
        var service = new BranchSynchronizationServiceV2();
        await service.RegisterContractAsync("ITest1", "content1", "Namespace1");
        await service.RegisterContractAsync("ITest2", "content2", "Namespace2");

        // Act
        var snapshot = service.GetContractSnapshot();

        // Assert
        Assert.Equal(2, snapshot.Count);
    }
}
