using AoTEngine.Models;
using AoTEngine.Services;
using Xunit;

namespace AoTEngine.Tests;

public class IntegrationCheckpointHandlerTests
{
    private readonly IntegrationCheckpointHandler _handler;

    public IntegrationCheckpointHandlerTests()
    {
        // Create handler in non-interactive mode for testing
        _handler = new IntegrationCheckpointHandler(interactive: false);
    }

    [Fact]
    public void GenerateConflictReports_ShouldCreateReportsForAllConflicts()
    {
        // Arrange
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "MyApp.IService",
                ConflictType = ConflictType.DuplicateType,
                SuggestedResolution = ConflictResolution.KeepFirst,
                ExistingEntry = new TypeRegistryEntry
                {
                    TypeName = "IService",
                    Namespace = "MyApp",
                    Kind = ProjectTypeKind.Interface,
                    OwnerTaskId = "task1"
                },
                NewEntry = new TypeRegistryEntry
                {
                    TypeName = "IService",
                    Namespace = "MyApp",
                    Kind = ProjectTypeKind.Interface,
                    OwnerTaskId = "task2"
                }
            }
        };

        // Act
        var reports = _handler.GenerateConflictReports(conflicts);

        // Assert
        Assert.Single(reports);
        Assert.Equal("MyApp.IService", reports[0].Conflict.FullyQualifiedName);
        Assert.NotEmpty(reports[0].Description);
        Assert.NotEmpty(reports[0].Options);
    }

    [Fact]
    public void FormatConflictReportForDisplay_ShouldGenerateReadableOutput()
    {
        // Arrange
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "MyApp.MyClass",
                ConflictType = ConflictType.DuplicateType,
                SuggestedResolution = ConflictResolution.MergeAsPartial,
                ExistingEntry = new TypeRegistryEntry
                {
                    TypeName = "MyClass",
                    Namespace = "MyApp",
                    Kind = ProjectTypeKind.Class,
                    OwnerTaskId = "task1"
                },
                NewEntry = new TypeRegistryEntry
                {
                    TypeName = "MyClass",
                    Namespace = "MyApp",
                    Kind = ProjectTypeKind.Class,
                    OwnerTaskId = "task2"
                }
            }
        };
        var reports = _handler.GenerateConflictReports(conflicts);

        // Act
        var display = _handler.FormatConflictReportForDisplay(reports);

        // Assert
        Assert.Contains("INTEGRATION CONFLICTS DETECTED", display);
        Assert.Contains("MyApp.MyClass", display);
        Assert.Contains("task1", display);
        Assert.Contains("task2", display);
        Assert.Contains("Available options", display);
    }

    [Fact]
    public async Task PromptForResolutionAsync_InNonInteractiveMode_ShouldUseRecommendedResolutions()
    {
        // Arrange
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "MyApp.IService",
                ConflictType = ConflictType.DuplicateType,
                SuggestedResolution = ConflictResolution.KeepFirst,
                ExistingEntry = new TypeRegistryEntry
                {
                    TypeName = "IService",
                    Kind = ProjectTypeKind.Interface,
                    OwnerTaskId = "task1"
                },
                NewEntry = new TypeRegistryEntry
                {
                    TypeName = "IService",
                    Kind = ProjectTypeKind.Interface,
                    OwnerTaskId = "task2"
                }
            }
        };
        var reports = _handler.GenerateConflictReports(conflicts);

        // Act
        var result = await _handler.PromptForResolutionAsync(reports);

        // Assert
        Assert.True(result.Continue);
        Assert.False(result.Abort);
        Assert.Contains("MyApp.IService", result.Resolutions.Keys);
        Assert.Equal(ConflictResolution.KeepFirst, result.Resolutions["MyApp.IService"]);
    }

    [Fact]
    public void RequiresManualIntervention_DuplicateMember_ShouldReturnTrue()
    {
        // Arrange
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "MyApp.MyClass",
                ConflictType = ConflictType.DuplicateMember,
                SuggestedResolution = ConflictResolution.RemoveDuplicate,
                ConflictingMembers = new List<MemberSignature>
                {
                    new MemberSignature { Name = "MyClass", Kind = ProjectMemberKind.Constructor }
                },
                ExistingEntry = new TypeRegistryEntry(),
                NewEntry = new TypeRegistryEntry()
            }
        };

        // Act
        var result = _handler.RequiresManualIntervention(conflicts);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RequiresManualIntervention_FailFast_ShouldReturnTrue()
    {
        // Arrange
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "MyApp.MyClass",
                ConflictType = ConflictType.DuplicateType,
                SuggestedResolution = ConflictResolution.FailFast,
                ExistingEntry = new TypeRegistryEntry(),
                NewEntry = new TypeRegistryEntry()
            }
        };

        // Act
        var result = _handler.RequiresManualIntervention(conflicts);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RequiresManualIntervention_SimpleKeepFirst_ShouldReturnFalse()
    {
        // Arrange
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "MyApp.IService",
                ConflictType = ConflictType.DuplicateType,
                SuggestedResolution = ConflictResolution.KeepFirst,
                ExistingEntry = new TypeRegistryEntry(),
                NewEntry = new TypeRegistryEntry()
            }
        };

        // Act
        var result = _handler.RequiresManualIntervention(conflicts);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GenerateConflictReports_ForClass_ShouldIncludePartialMergeOption()
    {
        // Arrange
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "MyApp.MyClass",
                ConflictType = ConflictType.DuplicateType,
                SuggestedResolution = ConflictResolution.MergeAsPartial,
                ExistingEntry = new TypeRegistryEntry
                {
                    TypeName = "MyClass",
                    Kind = ProjectTypeKind.Class,
                    OwnerTaskId = "task1"
                },
                NewEntry = new TypeRegistryEntry
                {
                    TypeName = "MyClass",
                    Kind = ProjectTypeKind.Class,
                    OwnerTaskId = "task2"
                }
            }
        };

        // Act
        var reports = _handler.GenerateConflictReports(conflicts);

        // Assert
        Assert.Contains(reports[0].Options, o => o.Resolution == ConflictResolution.MergeAsPartial);
    }

    [Fact]
    public void GenerateConflictReports_ForInterface_ShouldNotIncludePartialMergeOption()
    {
        // Arrange
        var conflicts = new List<TypeConflict>
        {
            new TypeConflict
            {
                FullyQualifiedName = "MyApp.IService",
                ConflictType = ConflictType.DuplicateType,
                SuggestedResolution = ConflictResolution.KeepFirst,
                ExistingEntry = new TypeRegistryEntry
                {
                    TypeName = "IService",
                    Kind = ProjectTypeKind.Interface,
                    OwnerTaskId = "task1"
                },
                NewEntry = new TypeRegistryEntry
                {
                    TypeName = "IService",
                    Kind = ProjectTypeKind.Interface,
                    OwnerTaskId = "task2"
                }
            }
        };

        // Act
        var reports = _handler.GenerateConflictReports(conflicts);

        // Assert
        Assert.DoesNotContain(reports[0].Options, o => o.Resolution == ConflictResolution.MergeAsPartial);
    }
}
