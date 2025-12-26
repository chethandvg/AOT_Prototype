using AoTEngine.Models;
using AoTEngine.Services.Aggregation;

namespace AoTEngine.Tests;

public class HierarchicalAggregationServiceV2Tests
{
    [Fact]
    public async Task AggregateTree_AtomicNode_ReturnsAtomicResult()
    {
        // Arrange
        var service = new HierarchicalAggregationServiceV2();
        var atomicNode = new ResponseChainNode
        {
            NodeId = "node1",
            IsAtomic = true,
            ExecutionResult = new AtomicExecutionResult
            {
                GeneratedCode = "public class Test { }",
                Summary = "Test class"
            }
        };

        // Act
        var result = await service.AggregateTreeAsync(atomicNode);

        // Assert
        Assert.Equal(AggregationType.Atomic, result.Type);
        Assert.Contains("public class Test", result.Content);
        Assert.True(result.DetailPreserved);
    }

    [Fact]
    public async Task AggregateTree_CompositeNode_AggregatesChildren()
    {
        // Arrange
        var service = new HierarchicalAggregationServiceV2();
        var child1 = new ResponseChainNode
        {
            NodeId = "child1",
            IsAtomic = true,
            ExecutionResult = new AtomicExecutionResult
            {
                GeneratedCode = "// Child 1 code",
                Summary = "Child 1"
            }
        };
        var child2 = new ResponseChainNode
        {
            NodeId = "child2",
            IsAtomic = true,
            ExecutionResult = new AtomicExecutionResult
            {
                GeneratedCode = "// Child 2 code",
                Summary = "Child 2"
            }
        };
        var parent = new ResponseChainNode
        {
            NodeId = "parent",
            IsAtomic = false,
            Children = new List<ResponseChainNode> { child1, child2 }
        };

        // Act
        var result = await service.AggregateTreeAsync(parent);

        // Assert
        Assert.Equal(AggregationType.Composite, result.Type);
        Assert.Equal(2, result.Children.Count);
        Assert.Contains("Child 1 code", result.Content);
        Assert.Contains("Child 2 code", result.Content);
    }

    [Fact]
    public void CollectAtomicNodes_ReturnsOnlyAtomicNodes()
    {
        // Arrange
        var service = new HierarchicalAggregationServiceV2();
        var atomic1 = new ResponseChainNode { NodeId = "a1", IsAtomic = true };
        var atomic2 = new ResponseChainNode { NodeId = "a2", IsAtomic = true };
        var composite = new ResponseChainNode
        {
            NodeId = "c1",
            IsAtomic = false,
            Children = new List<ResponseChainNode> { atomic1, atomic2 }
        };

        // Act
        var atomicNodes = service.CollectAtomicNodes(composite);

        // Assert
        Assert.Equal(2, atomicNodes.Count);
        Assert.All(atomicNodes, n => Assert.True(n.IsAtomic));
    }
}
