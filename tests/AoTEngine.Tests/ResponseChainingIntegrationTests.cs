using AoTEngine.Core;
using AoTEngine.Models;
using AoTEngine.Services;
using AoTEngine.Services.AI;
using AoTEngine.Services.Aggregation;
using AoTEngine.Services.Contracts;
using AoTEngine.Services.Recovery;

namespace AoTEngine.Tests;

/// <summary>
/// Integration tests for the complete response chaining flow.
/// These tests demonstrate the hierarchical decomposition → execution → aggregation pipeline.
/// </summary>
public class ResponseChainingIntegrationTests
{
    [Fact]
    public void HierarchicalDecomposition_ComponentIntegration_AllServicesWork()
    {
        // This test verifies that all components work together
        // Note: Actual OpenAI calls are not made in this test

        // Arrange
        var syncService = new BranchSynchronizationServiceV2();
        var aggregationService = new HierarchicalAggregationServiceV2();
        var checkpointService = new CheckpointRecoveryServiceV2();
        var graphManager = new DependencyGraphManagerV2();

        // Create a sample decomposition tree
        var root = new ResponseChainNode
        {
            NodeId = "root",
            TaskId = "main_task",
            Depth = 0,
            IsAtomic = false
        };

        var child1 = new ResponseChainNode
        {
            NodeId = "child1",
            TaskId = "subtask1",
            Depth = 1,
            IsAtomic = true,
            ExecutionResult = new AtomicExecutionResult
            {
                GeneratedCode = "public interface IService { void Execute(); }",
                Summary = "Service interface"
            }
        };

        var child2 = new ResponseChainNode
        {
            NodeId = "child2",
            TaskId = "subtask2",
            Depth = 1,
            IsAtomic = true,
            ExecutionResult = new AtomicExecutionResult
            {
                GeneratedCode = "public class ServiceImpl : IService { public void Execute() { } }",
                Summary = "Service implementation"
            }
        };

        root.Children.Add(child1);
        root.Children.Add(child2);

        // Act & Assert

        // 1. Verify branch synchronization works
        var syncResult = syncService.RegisterContractAsync(
            "IService",
            "public interface IService { void Execute(); }",
            "TestNamespace"
        ).Result;
        Assert.True(syncResult.Success);

        // 2. Verify aggregation works
        var aggregationResult = aggregationService.AggregateTreeAsync(root).Result;
        Assert.Equal(AggregationType.Composite, aggregationResult.Type);
        Assert.Equal(2, aggregationResult.Children.Count);

        // 3. Verify checkpoint works
        var snapshot = new SerializableTreeSnapshot
        {
            Root = root,
            AllNodes = new List<ResponseChainNode> { root, child1, child2 },
            MaxDepth = 1,
            AtomicNodeCount = 2
        };
        var checkpointPath = checkpointService.SaveCheckpointAsync(snapshot).Result;
        Assert.True(File.Exists(checkpointPath));

        // 4. Verify recovery works
        var recoveryResult = checkpointService.RecoverFromCheckpointAsync(checkpointPath).Result;
        Assert.True(recoveryResult.Success);
        Assert.NotNull(recoveryResult.RecoveredSnapshot);

        // 5. Verify dependency graph works
        graphManager.RegisterTask("subtask1");
        graphManager.RegisterTask("subtask2");
        var depResult = graphManager.AddDependency("subtask2", "subtask1");
        Assert.True(depResult.Success);

        graphManager.MarkCompleted("subtask1");
        var readyTasks = graphManager.GetReadyTasks();
        Assert.Contains("subtask2", readyTasks);

        // Cleanup
        if (File.Exists(checkpointPath))
        {
            File.Delete(checkpointPath);
        }
    }

    [Fact]
    public async Task ResponseChainService_CreateAndAggregateTree_Succeeds()
    {
        // This test demonstrates the ResponseChainService workflow
        // without actual OpenAI calls

        // Arrange - Create mock OpenAI service (would need proper DI in real scenario)
        // For this test, we'll just verify the tree structure

        var root = new ResponseChainNode
        {
            NodeId = "root",
            TaskId = "project",
            Depth = 0,
            IsAtomic = false
        };

        // Create a multi-level tree
        var level1_1 = new ResponseChainNode
        {
            NodeId = "l1_1",
            TaskId = "models",
            Depth = 1,
            IsAtomic = false
        };

        var level1_2 = new ResponseChainNode
        {
            NodeId = "l1_2",
            TaskId = "services",
            Depth = 1,
            IsAtomic = false
        };

        var level2_1 = new ResponseChainNode
        {
            NodeId = "l2_1",
            TaskId = "user_model",
            Depth = 2,
            IsAtomic = true,
            ExecutionResult = new AtomicExecutionResult
            {
                GeneratedCode = "public class User { public string Name { get; set; } }",
                Summary = "User model"
            }
        };

        var level2_2 = new ResponseChainNode
        {
            NodeId = "l2_2",
            TaskId = "user_service",
            Depth = 2,
            IsAtomic = true,
            ExecutionResult = new AtomicExecutionResult
            {
                GeneratedCode = "public class UserService { }",
                Summary = "User service"
            }
        };

        level1_1.Children.Add(level2_1);
        level1_2.Children.Add(level2_2);
        root.Children.Add(level1_1);
        root.Children.Add(level1_2);

        var aggregationService = new HierarchicalAggregationServiceV2();

        // Act
        var result = await aggregationService.AggregateTreeAsync(root);

        // Assert
        Assert.Equal(AggregationType.Composite, result.Type);
        Assert.Contains("User model", result.Summary);
        Assert.Contains("User service", result.Summary);

        // Verify atomic node collection
        var atomicNodes = aggregationService.CollectAtomicNodes(root);
        Assert.Equal(2, atomicNodes.Count);
        Assert.All(atomicNodes, n => Assert.True(n.IsAtomic));
    }

    [Fact]
    public void DependencyGraphManager_ComplexWorkflow_HandlesAllScenarios()
    {
        // Test a complex scenario with multiple dependency patterns

        // Arrange
        var manager = new DependencyGraphManagerV2(FailurePolicy.SkipFailed);

        // Create a diamond dependency pattern
        //     A
        //    / \
        //   B   C
        //    \ /
        //     D

        manager.RegisterTask("A");
        manager.RegisterTask("B");
        manager.RegisterTask("C");
        manager.RegisterTask("D");

        manager.AddDependency("B", "A");
        manager.AddDependency("C", "A");
        manager.AddDependency("D", "B");
        manager.AddDependency("D", "C");

        // Act & Assert

        // Wave 1: Only A should be ready
        var wave1 = manager.GetReadyTasks();
        Assert.Single(wave1);
        Assert.Contains("A", wave1);

        // Complete A
        manager.MarkCompleted("A");

        // Wave 2: B and C should be ready
        var wave2 = manager.GetReadyTasks();
        Assert.Equal(2, wave2.Count);
        Assert.Contains("B", wave2);
        Assert.Contains("C", wave2);

        // Complete B and C
        manager.MarkCompleted("B");
        manager.MarkCompleted("C");

        // Wave 3: D should be ready
        var wave3 = manager.GetReadyTasks();
        Assert.Contains("D", wave3);

        // Verify execution plan
        var plan = manager.GenerateExecutionPlan();
        Assert.True(plan.Waves.Count >= 3);

        // Verify critical path
        var criticalPath = manager.CalculateCriticalPath();
        Assert.NotEmpty(criticalPath);
    }

    [Fact]
    public async Task ContextCompression_LargeContext_CompressesEffectively()
    {
        // Test context compression with a large code sample

        // Arrange
        var service = new ContextCompressionServiceV2(
            maxContextTokens: 1000,
            compressionThreshold: 500
        );

        var largeContext = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public interface IUserService
    {
        User GetUser(int id);
        void CreateUser(User user);
        void UpdateUser(User user);
        void DeleteUser(int id);
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserService : IUserService
    {
        private readonly IRepository<User> _repository;

        public UserService(IRepository<User> repository)
        {
            _repository = repository;
        }

        public User GetUser(int id)
        {
            return _repository.GetById(id);
        }

        public void CreateUser(User user)
        {
            _repository.Add(user);
        }

        public void UpdateUser(User user)
        {
            _repository.Update(user);
        }

        public void DeleteUser(int id)
        {
            _repository.Delete(id);
        }
    }
}";

        // Act
        var result = await service.CompressContextAsync(
            largeContext,
            new List<string> { "IUserService", "User" }
        );

        // Assert
        Assert.True(result.VerificationPassed);
        Assert.Contains("IUserService", result.CompressedText);
        Assert.Contains("User", result.CompressedText);

        // Compression should reduce size or stay same for small inputs
        Assert.True(result.CompressedTokenCount <= result.OriginalTokenCount);
    }
}
