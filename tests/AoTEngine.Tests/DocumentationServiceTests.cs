using AoTEngine.Models;
using AoTEngine.Services;
using Xunit;

namespace AoTEngine.Tests;

public class DocumentationServiceTests
{
    [Fact]
    public void TaskSummaryRecord_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var record = new TaskSummaryRecord();

        // Assert
        Assert.NotNull(record.TaskId);
        Assert.NotNull(record.TaskDescription);
        Assert.NotNull(record.Dependencies);
        Assert.Empty(record.Dependencies);
        Assert.NotNull(record.ExpectedTypes);
        Assert.Empty(record.ExpectedTypes);
        Assert.NotNull(record.KeyBehaviors);
        Assert.NotNull(record.EdgeCases);
        Assert.NotNull(record.GeneratedCode);
    }

    [Fact]
    public void TaskSummaryRecord_ShouldSetProperties()
    {
        // Arrange & Act
        var record = new TaskSummaryRecord
        {
            TaskId = "task1",
            TaskDescription = "Test task",
            Dependencies = new List<string> { "task0" },
            ExpectedTypes = new List<string> { "TestClass" },
            Namespace = "Test.Namespace",
            Purpose = "Tests something",
            KeyBehaviors = new List<string> { "Does X", "Does Y" },
            EdgeCases = new List<string> { "Handles null" },
            ValidationNotes = "Passed on first attempt",
            GeneratedCodeHash = "ABC123"
        };

        // Assert
        Assert.Equal("task1", record.TaskId);
        Assert.Equal("Test task", record.TaskDescription);
        Assert.Single(record.Dependencies);
        Assert.Equal("task0", record.Dependencies[0]);
        Assert.Single(record.ExpectedTypes);
        Assert.Equal("TestClass", record.ExpectedTypes[0]);
        Assert.Equal("Test.Namespace", record.Namespace);
        Assert.Equal("Tests something", record.Purpose);
        Assert.Equal(2, record.KeyBehaviors.Count);
        Assert.Single(record.EdgeCases);
        Assert.Equal("Passed on first attempt", record.ValidationNotes);
        Assert.Equal("ABC123", record.GeneratedCodeHash);
    }

    [Fact]
    public void ProjectDocumentation_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var doc = new ProjectDocumentation();

        // Assert
        Assert.NotNull(doc.ProjectRequest);
        Assert.NotNull(doc.GlobalAssumptions);
        Assert.Empty(doc.GlobalAssumptions);
        Assert.NotNull(doc.TaskRecords);
        Assert.Empty(doc.TaskRecords);
        Assert.NotNull(doc.ModuleIndex);
        Assert.Empty(doc.ModuleIndex);
        Assert.NotNull(doc.HighLevelArchitectureSummary);
        Assert.NotNull(doc.DependencyGraphSummary);
    }

    [Fact]
    public void ProjectDocumentation_ShouldSetProperties()
    {
        // Arrange & Act
        var doc = new ProjectDocumentation
        {
            ProjectRequest = "Create a calculator",
            Description = "A simple calculator project",
            HighLevelArchitectureSummary = "Uses service pattern",
            DependencyGraphSummary = "Task1 -> Task2"
        };
        
        doc.GlobalAssumptions.Add("Assumes integer arithmetic");
        doc.TaskRecords.Add(new TaskSummaryRecord { TaskId = "task1" });
        doc.ModuleIndex["Calculator"] = "task1";

        // Assert
        Assert.Equal("Create a calculator", doc.ProjectRequest);
        Assert.Equal("A simple calculator project", doc.Description);
        Assert.Equal("Uses service pattern", doc.HighLevelArchitectureSummary);
        Assert.Equal("Task1 -> Task2", doc.DependencyGraphSummary);
        Assert.Single(doc.GlobalAssumptions);
        Assert.Single(doc.TaskRecords);
        Assert.Single(doc.ModuleIndex);
        Assert.Equal("task1", doc.ModuleIndex["Calculator"]);
    }

    [Fact]
    public void TaskNode_ShouldHaveNewDocumentationFields()
    {
        // Arrange & Act
        var task = new TaskNode
        {
            Id = "task1",
            Summary = "This task creates a calculator class",
            SummaryModel = "gpt-4o-mini",
            ValidationAttemptCount = 2,
            SummaryGeneratedAtUtc = DateTime.UtcNow
        };

        // Assert
        Assert.Equal("task1", task.Id);
        Assert.Equal("This task creates a calculator class", task.Summary);
        Assert.Equal("gpt-4o-mini", task.SummaryModel);
        Assert.Equal(2, task.ValidationAttemptCount);
        Assert.NotNull(task.SummaryGeneratedAtUtc);
    }

    [Fact]
    public void DocumentationConfig_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var config = new DocumentationConfig();

        // Assert
        Assert.True(config.Enabled);
        Assert.True(config.GeneratePerTask);
        Assert.True(config.GenerateProjectSummary);
        Assert.True(config.ExportMarkdown);
        Assert.True(config.ExportJson);
        Assert.True(config.ExportJsonl);
        Assert.Equal("gpt-4o-mini", config.SummaryModel);
        Assert.Equal(300, config.MaxSummaryTokens);
        Assert.Equal(3, config.MaxConcurrentSummaryCalls);
    }

    [Fact]
    public void DocumentationConfig_ShouldAllowDisabling()
    {
        // Arrange & Act
        var config = new DocumentationConfig
        {
            Enabled = false,
            GeneratePerTask = false,
            ExportMarkdown = false,
            ExportJson = false,
            ExportJsonl = false
        };

        // Assert
        Assert.False(config.Enabled);
        Assert.False(config.GeneratePerTask);
        Assert.False(config.ExportMarkdown);
        Assert.False(config.ExportJson);
        Assert.False(config.ExportJsonl);
    }
}
