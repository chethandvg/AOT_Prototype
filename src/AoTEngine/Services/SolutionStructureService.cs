using AoTEngine.Models;
using AoTEngine.Services.AI;
using AoTEngine.Services.Aggregation;

namespace AoTEngine.Services;

/// <summary>
/// Service for generating solution structure from aggregated results.
/// </summary>
public class SolutionStructureService
{
    private readonly ResponseChainService _chainService;
    private readonly HierarchicalAggregationServiceV2 _aggregationService;
    private readonly OpenAIService _openAIService;

    public SolutionStructureService(
        ResponseChainService chainService,
        HierarchicalAggregationServiceV2? aggregationService = null,
        OpenAIService? openAIService = null)
    {
        _chainService = chainService;
        _aggregationService = aggregationService ?? new HierarchicalAggregationServiceV2();
        _openAIService = openAIService ?? throw new ArgumentNullException(nameof(openAIService));
    }

    /// <summary>
    /// Generates solution structure from decomposition tree.
    /// </summary>
    public async Task<SolutionStructure> GenerateStructureAsync(ResponseChainNode root)
    {
        // Collect atomic nodes
        var atomicNodes = _aggregationService.CollectAtomicNodes(root);

        Console.WriteLine($"📊 Generating solution structure from {atomicNodes.Count} atomic tasks");

        // Aggregate results
        var aggregatedResult = await _aggregationService.AggregateTreeAsync(root);

        // Generate project structure
        var structure = new SolutionStructure
        {
            ProjectName = "GeneratedSolution",
            RootPath = "./output",
            Files = new List<FileStructure>()
        };

        // Create files from atomic nodes
        foreach (var node in atomicNodes)
        {
            if (node.ExecutionResult?.GeneratedCode != null)
            {
                var file = new FileStructure
                {
                    Path = $"{node.TaskId}.cs",
                    Content = node.ExecutionResult.GeneratedCode,
                    NodeId = node.NodeId
                };

                structure.Files.Add(file);
            }
        }

        Console.WriteLine($"✅ Generated structure with {structure.Files.Count} files");

        return structure;
    }

    /// <summary>
    /// Creates atomic tasks for project structure generation.
    /// </summary>
    public List<TaskNode> CreateStructureTasks(SolutionStructure structure)
    {
        var tasks = new List<TaskNode>();

        // Create task for each file
        foreach (var file in structure.Files)
        {
            var task = new TaskNode
            {
                Id = $"file_{Path.GetFileNameWithoutExtension(file.Path)}",
                Description = $"Generate file: {file.Path}",
                Context = file.Content,
                GeneratedCode = file.Content,
                IsCompleted = true,
                IsValidated = false
            };

            tasks.Add(task);
        }

        return tasks;
    }

    /// <summary>
    /// Handles file dependencies in code generation.
    /// </summary>
    public Dictionary<string, List<string>> AnalyzeDependencies(SolutionStructure structure)
    {
        var dependencies = new Dictionary<string, List<string>>();

        // Simple dependency analysis based on using statements
        foreach (var file in structure.Files)
        {
            var fileDeps = new List<string>();
            var lines = file.Content.Split('\n');

            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("using ") && !line.Contains("System"))
                {
                    // Extract namespace from using statement
                    var parts = line.Split(' ', ';');
                    if (parts.Length >= 2)
                    {
                        var ns = parts[1].TrimEnd(';');
                        fileDeps.Add(ns);
                    }
                }
            }

            if (fileDeps.Count > 0)
            {
                dependencies[file.Path] = fileDeps;
            }
        }

        return dependencies;
    }
}

/// <summary>
/// Represents the structure of a solution.
/// </summary>
public class SolutionStructure
{
    public string ProjectName { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public List<FileStructure> Files { get; set; } = new();
}

/// <summary>
/// Represents a file in the solution structure.
/// </summary>
public class FileStructure
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
}
