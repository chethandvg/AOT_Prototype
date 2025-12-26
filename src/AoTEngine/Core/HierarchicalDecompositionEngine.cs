using AoTEngine.Models;
using AoTEngine.Services;
using AoTEngine.Services.AI;
using AoTEngine.Services.Recovery;

namespace AoTEngine.Core;

/// <summary>
/// Engine for hierarchical recursive decomposition with atomicity detection.
/// </summary>
public class HierarchicalDecompositionEngine
{
    private readonly OpenAIService _openAIService;
    private readonly TaskComplexityAnalyzer _complexityAnalyzer;
    private readonly ResponseChainService _chainService;
    private readonly CheckpointRecoveryServiceV2 _checkpointService;
    private readonly int _maxDecompositionDepth;
    private readonly int _atomicComplexityThreshold;

    public HierarchicalDecompositionEngine(
        OpenAIService openAIService,
        ResponseChainService? chainService = null,
        CheckpointRecoveryServiceV2? checkpointService = null,
        int maxDecompositionDepth = 5,
        int atomicComplexityThreshold = 30)
    {
        _openAIService = openAIService;
        _complexityAnalyzer = new TaskComplexityAnalyzer();
        _chainService = chainService ?? new ResponseChainService(openAIService);
        _checkpointService = checkpointService ?? new CheckpointRecoveryServiceV2();
        _maxDecompositionDepth = maxDecompositionDepth;
        _atomicComplexityThreshold = atomicComplexityThreshold;
    }

    /// <summary>
    /// Decomposes a request hierarchically until all tasks are atomic.
    /// </summary>
    public async Task<ResponseChainNode> DecomposeRecursivelyAsync(
        string userRequest,
        string projectDescription)
    {
        // Initiate the response chain
        var rootNode = await _chainService.InitiateChainAsync(userRequest, projectDescription);

        // Start recursive decomposition
        await DecomposeNodeAsync(rootNode, userRequest, projectDescription);

        // Save checkpoint
        var snapshot = _chainService.CreateSnapshot(rootNode);
        await _checkpointService.SaveCheckpointAsync(snapshot, "Final decomposition");

        return rootNode;
    }

    /// <summary>
    /// Recursively decomposes a node until it's atomic.
    /// </summary>
    private async Task DecomposeNodeAsync(
        ResponseChainNode node,
        string description,
        string context,
        int currentDepth = 0)
    {
        // Check max depth protection
        if (currentDepth >= _maxDecompositionDepth)
        {
            Console.WriteLine($"⚠️  Max decomposition depth {_maxDecompositionDepth} reached for node {node.NodeId}");
            node.IsAtomic = true;
            return;
        }

        // Create a task node to analyze complexity
        var taskNode = new TaskNode
        {
            Id = node.TaskId,
            Description = description,
            Context = context
        };

        // Analyze complexity
        var metrics = _complexityAnalyzer.AnalyzeTask(taskNode, 300);
        node.Metrics = metrics;

        // Check if atomic based on complexity threshold
        if (metrics.ComplexityScore <= _atomicComplexityThreshold)
        {
            Console.WriteLine($"✅ Node {node.NodeId} is atomic (complexity: {metrics.ComplexityScore})");
            node.IsAtomic = true;
            return;
        }

        // Task is complex, decompose it
        Console.WriteLine($"🔄 Decomposing node {node.NodeId} (complexity: {metrics.ComplexityScore}, depth: {currentDepth})");

        // Decompose using OpenAI
        var subtasks = await DecomposeTaskAsync(taskNode, metrics.RecommendedSubtaskCount);

        // Create child nodes
        var childNodes = new List<ResponseChainNode>();
        foreach (var subtask in subtasks)
        {
            var childNode = new ResponseChainNode
            {
                NodeId = Guid.NewGuid().ToString(),
                TaskId = subtask.Id,
                ParentResponseId = node.ResponseId,
                Depth = currentDepth + 1
            };

            childNodes.Add(childNode);
            node.Children.Add(childNode);

            // Recursively decompose child
            await DecomposeNodeAsync(
                childNode,
                subtask.Description,
                subtask.Context,
                currentDepth + 1);
        }

        // Save checkpoint periodically
        if (currentDepth % 2 == 0)
        {
            var snapshot = _chainService.CreateSnapshot(FindRoot(node));
            await _checkpointService.SaveCheckpointAsync(snapshot, $"Decomposition at depth {currentDepth}");
        }
    }

    /// <summary>
    /// Decomposes a task into subtasks using OpenAI.
    /// </summary>
    private async Task<List<TaskNode>> DecomposeTaskAsync(TaskNode task, int recommendedSubtaskCount)
    {
        try
        {
            // Use existing OpenAI decomposition
            var subtasks = await _openAIService.DecomposeComplexTaskAsync(
                task,
                recommendedSubtaskCount,
                300);

            return subtasks;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Failed to decompose task {task.Id}: {ex.Message}");
            // Return single task as fallback
            return new List<TaskNode> { task };
        }
    }

    /// <summary>
    /// Finds the root node of a tree.
    /// </summary>
    private ResponseChainNode FindRoot(ResponseChainNode node)
    {
        // In a real implementation, would traverse up to root
        // For now, just return the node
        return node;
    }

    /// <summary>
    /// Collects all atomic nodes from the tree.
    /// </summary>
    public List<ResponseChainNode> CollectAtomicNodes(ResponseChainNode root)
    {
        var atomicNodes = new List<ResponseChainNode>();
        CollectAtomicNodesRecursive(root, atomicNodes);
        return atomicNodes;
    }

    private void CollectAtomicNodesRecursive(ResponseChainNode node, List<ResponseChainNode> atomicNodes)
    {
        if (node.IsAtomic)
        {
            atomicNodes.Add(node);
        }

        foreach (var child in node.Children)
        {
            CollectAtomicNodesRecursive(child, atomicNodes);
        }
    }

    /// <summary>
    /// Converts decomposition tree to flat task list for execution.
    /// </summary>
    public List<TaskNode> ConvertToTaskList(ResponseChainNode root)
    {
        var atomicNodes = CollectAtomicNodes(root);
        var tasks = new List<TaskNode>();

        foreach (var node in atomicNodes)
        {
            var task = new TaskNode
            {
                Id = node.TaskId,
                Description = node.ExecutionResult?.Summary ?? $"Task {node.TaskId}",
                Context = node.ExecutionResult?.GeneratedCode ?? "",
                GeneratedCode = node.ExecutionResult?.GeneratedCode ?? "",
                IsCompleted = node.ExecutionResult != null,
                IsValidated = node.ExecutionResult?.IsValidated ?? false
            };

            tasks.Add(task);
        }

        return tasks;
    }
}
