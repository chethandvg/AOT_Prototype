using AoTEngine.Models;

namespace AoTEngine.Services.Aggregation;

/// <summary>
/// Service for hierarchically aggregating results with memory efficiency.
/// </summary>
public class HierarchicalAggregationServiceV2
{
    private readonly int _maxSummaryDepth;
    private readonly bool _preserveAtomicDetails;
    private readonly int _maxCompositeSummaryTokens;
    private readonly string? _externalStoragePath;

    public HierarchicalAggregationServiceV2(
        int maxSummaryDepth = 3,
        bool preserveAtomicDetails = true,
        int maxCompositeSummaryTokens = 500,
        string? externalStoragePath = null)
    {
        _maxSummaryDepth = maxSummaryDepth;
        _preserveAtomicDetails = preserveAtomicDetails;
        _maxCompositeSummaryTokens = maxCompositeSummaryTokens;
        _externalStoragePath = externalStoragePath ?? Path.Combine(Path.GetTempPath(), "aot_aggregation");

        if (!Directory.Exists(_externalStoragePath))
        {
            Directory.CreateDirectory(_externalStoragePath);
        }
    }

    /// <summary>
    /// Aggregates results from a decomposition tree using streaming.
    /// </summary>
    public async Task<AggregatedResult> AggregateTreeAsync(ResponseChainNode root)
    {
        return await AggregateNodeAsync(root, 0);
    }

    private async Task<AggregatedResult> AggregateNodeAsync(ResponseChainNode node, int currentDepth)
    {
        // If atomic, return atomic result
        if (node.IsAtomic && node.ExecutionResult != null)
        {
            var tokenCount = EstimateTokenCount(node.ExecutionResult.GeneratedCode);
            var content = node.ExecutionResult.GeneratedCode;
            string? externalRef = null;

            // Store to external storage if too large
            if (tokenCount > _maxCompositeSummaryTokens && currentDepth > _maxSummaryDepth)
            {
                externalRef = await StoreToExternalStorageAsync(content, node.NodeId);
                content = _preserveAtomicDetails ? content : node.ExecutionResult.Summary ?? "";
            }

            return new AggregatedResult
            {
                Type = AggregationType.Atomic,
                Content = content,
                Summary = node.ExecutionResult.Summary ?? "",
                TokenCount = tokenCount,
                DetailPreserved = _preserveAtomicDetails || currentDepth <= _maxSummaryDepth,
                ExternalStorageRef = externalRef
            };
        }

        // Composite: aggregate children
        var childResults = new List<AggregatedResult>();
        foreach (var child in node.Children)
        {
            var childResult = await AggregateNodeAsync(child, currentDepth + 1);
            childResults.Add(childResult);
        }

        // Combine child results
        var combinedContent = string.Join("\n\n", childResults.Select(r => r.Content));
        var combinedSummary = string.Join("\n", childResults.Select(r => r.Summary));
        var totalTokens = childResults.Sum(r => r.TokenCount);

        // Compress if needed
        if (totalTokens > _maxCompositeSummaryTokens && currentDepth > _maxSummaryDepth)
        {
            combinedContent = combinedSummary;
            totalTokens = EstimateTokenCount(combinedContent);
        }

        return new AggregatedResult
        {
            Type = AggregationType.Composite,
            Content = combinedContent,
            Summary = combinedSummary,
            Children = childResults,
            TokenCount = totalTokens,
            DetailPreserved = currentDepth <= _maxSummaryDepth
        };
    }

    /// <summary>
    /// Stores content to external storage.
    /// </summary>
    private async Task<string> StoreToExternalStorageAsync(string content, string nodeId)
    {
        var filename = $"{nodeId}.txt";
        var path = Path.Combine(_externalStoragePath!, filename);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    /// <summary>
    /// Retrieves content from external storage.
    /// </summary>
    public async Task<string?> RetrieveFromExternalStorageAsync(string externalRef)
    {
        if (!File.Exists(externalRef))
        {
            return null;
        }

        return await File.ReadAllTextAsync(externalRef);
    }

    /// <summary>
    /// Estimates token count.
    /// </summary>
    private int EstimateTokenCount(string text)
    {
        return text.Length / 4; // Simple approximation
    }

    /// <summary>
    /// Collects all atomic nodes from a tree.
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
        else
        {
            foreach (var child in node.Children)
            {
                CollectAtomicNodesRecursive(child, atomicNodes);
            }
        }
    }
}
