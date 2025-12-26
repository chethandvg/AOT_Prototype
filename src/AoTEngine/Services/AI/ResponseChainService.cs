using AoTEngine.Models;
using AoTEngine.Services.Contracts;

namespace AoTEngine.Services.AI;

/// <summary>
/// Core service for managing response chains with hierarchical decomposition.
/// </summary>
public class ResponseChainService
{
    private readonly OpenAIService _openAIService;
    private readonly BranchSynchronizationServiceV2 _syncService;
    private readonly Dictionary<string, ResponseChainNode> _nodeRegistry = new();

    public ResponseChainService(
        OpenAIService openAIService,
        BranchSynchronizationServiceV2? syncService = null)
    {
        _openAIService = openAIService;
        _syncService = syncService ?? new BranchSynchronizationServiceV2();
    }

    /// <summary>
    /// Initiates a new response chain from a user request.
    /// </summary>
    public async Task<ResponseChainNode> InitiateChainAsync(string userRequest, string projectDescription)
    {
        var rootNode = new ResponseChainNode
        {
            NodeId = "root",
            TaskId = "initial_decomposition",
            Depth = 0,
            IsAtomic = false
        };

        _nodeRegistry[rootNode.NodeId] = rootNode;

        // Store initial request context (would normally call OpenAI here)
        await Task.CompletedTask;

        return rootNode;
    }

    /// <summary>
    /// Decomposes a task with parent response chaining.
    /// </summary>
    public async Task<List<ResponseChainNode>> DecomposeWithChainingAsync(
        ResponseChainNode parentNode,
        TaskNode task)
    {
        var childNodes = new List<ResponseChainNode>();

        // This would call OpenAI with parent response ID for context
        // For now, create placeholder child nodes
        var childTasks = new List<TaskNode> { task }; // Simplified

        foreach (var childTask in childTasks)
        {
            var childNode = new ResponseChainNode
            {
                NodeId = Guid.NewGuid().ToString(),
                TaskId = childTask.Id,
                ParentResponseId = parentNode.ResponseId,
                Depth = parentNode.Depth + 1,
                IsAtomic = false // Will be determined by complexity
            };

            _nodeRegistry[childNode.NodeId] = childNode;
            childNodes.Add(childNode);
        }

        parentNode.Children.AddRange(childNodes);

        await Task.CompletedTask;
        return childNodes;
    }

    /// <summary>
    /// Executes an atomic task with dependency context.
    /// </summary>
    public async Task<AtomicExecutionResult> ExecuteAtomicTaskAsync(
        ResponseChainNode node,
        TaskNode task,
        List<TaskNode> dependencies)
    {
        // Convert dependencies list to dictionary
        var dependencyDict = dependencies.ToDictionary(d => d.Id, d => d);

        // This would call OpenAI with chained context
        // For now, use the existing OpenAI service
        var generatedCode = await _openAIService.GenerateCodeAsync(task, dependencyDict);

        var result = new AtomicExecutionResult
        {
            GeneratedCode = generatedCode,
            IsValidated = false,
            Summary = $"Generated code for {task.Description}"
        };

        node.ExecutionResult = result;
        node.IsAtomic = true;
        node.CompletedAt = DateTime.UtcNow;

        return result;
    }

    /// <summary>
    /// Aggregates solution from atomic results.
    /// </summary>
    public async Task<string> AggregateSolutionAsync(ResponseChainNode root)
    {
        var atomicNodes = CollectAtomicNodes(root);
        var aggregatedCode = string.Join("\n\n// ========================================\n\n",
            atomicNodes.Select(n => n.ExecutionResult?.GeneratedCode ?? ""));

        await Task.CompletedTask;
        return aggregatedCode;
    }

    /// <summary>
    /// Gets a node by ID.
    /// </summary>
    public ResponseChainNode? GetNode(string nodeId)
    {
        return _nodeRegistry.TryGetValue(nodeId, out var node) ? node : null;
    }

    /// <summary>
    /// Collects all atomic nodes from the tree.
    /// </summary>
    private List<ResponseChainNode> CollectAtomicNodes(ResponseChainNode root)
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
    /// Creates a serializable snapshot of the tree.
    /// </summary>
    public SerializableTreeSnapshot CreateSnapshot(ResponseChainNode root)
    {
        var snapshot = new SerializableTreeSnapshot
        {
            Root = root,
            AllNodes = _nodeRegistry.Values.ToList(),
            ContractSnapshot = _syncService.GetContractSnapshot(),
            SnapshotTime = DateTime.UtcNow,
            MaxDepth = CalculateMaxDepth(root),
            AtomicNodeCount = CollectAtomicNodes(root).Count
        };

        return snapshot;
    }

    private int CalculateMaxDepth(ResponseChainNode node)
    {
        if (node.Children.Count == 0)
        {
            return node.Depth;
        }

        return node.Children.Max(c => CalculateMaxDepth(c));
    }
}
