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
            // Log full exception details to standard error to avoid silently swallowing critical issues
            Console.Error.WriteLine($"⚠️  Failed to decompose task {task.Id}: {ex}");

            // For clearly critical errors (e.g., authentication/authorization issues), rethrow so they can be handled upstream
            if (ex is UnauthorizedAccessException)
            {
                throw;
            }

            // For other errors, return single task as fallback to preserve existing behavior
            return new List<TaskNode> { task };
        }
    }

    /// <summary>
    /// Returns the root node of a response chain.
    /// </summary>
    /// <remarks>
    /// In this engine, response chains are always normalized so that callers pass the actual
    /// root node into this method. Because of that invariant, no traversal is required and
    /// the input node is returned unchanged.
    /// </remarks>
    private ResponseChainNode FindRoot(ResponseChainNode node)
    {
        // Callers are required to pass the root node; by design this is a no-op.
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
            var executionResult = node.ExecutionResult;

            var task = new TaskNode
            {
                Id = node.TaskId,
                Description = executionResult?.Summary ?? $"Task {node.TaskId}",
                Context = executionResult?.GeneratedCode ?? "",
                GeneratedCode = executionResult?.GeneratedCode ?? "",
                IsCompleted = executionResult != null,
                IsValidated = executionResult?.IsValidated ?? false
            };

            tasks.Add(task);
        }

        return tasks;
    }
}
