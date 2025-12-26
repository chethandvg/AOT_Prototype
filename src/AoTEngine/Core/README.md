# Core Layer

This folder contains the core orchestration and execution engine components for the AoT (Atom of Thought) Engine.

## Overview

The Core layer is the heart of the AoT Engine, coordinating all workflow steps from task decomposition to final code generation.

## Components

### AoTEngineOrchestrator

The main orchestrator that coordinates the entire AoT workflow.

**File:** `AoTEngineOrchestrator.cs`

**Workflow Steps:**
1. **Decompose request** - Break down user request into atomic tasks
2. **Contract generation** - Generate frozen API contracts (when `enableContractFirst=true`)
3. **Complexity analysis** - Analyze and decompose complex tasks (>300 lines)
4. **Execute tasks** - Run tasks in parallel with validation
5. **Validate contracts** - Ensure all dependencies are satisfied
6. **Merge code** - Combine task outputs into cohesive solution
7. **Generate report** - Create execution statistics
8. **Synthesize documentation** - Generate project documentation

### AoTResult

Result model returned from AoT Engine execution.

**File:** `AoTResult.cs`

**Properties:**
- `Success` - Whether execution completed successfully
- `FinalCode` - The merged generated code
- `Tasks` - List of all executed tasks
- `ExecutionReport` - Execution statistics
- `ProjectDocumentation` - Generated documentation
- `ContractCatalog` - Frozen contracts (when Contract-First enabled)

### ParallelExecutionEngine

Engine for executing tasks in parallel based on their dependencies.

**Files (partial classes):**

| File | Responsibility |
|------|----------------|
| `ParallelExecutionEngine.cs` | Core fields, constructor, complexity analysis |
| `ParallelExecutionEngine.BatchValidation.cs` | Batch validation mode |
| `ParallelExecutionEngine.HybridValidation.cs` | Hybrid validation mode |
| `ParallelExecutionEngine.TaskExecution.cs` | Individual task execution |
| `ParallelExecutionEngine.ProblemIdentification.cs` | Error analysis and problem identification |
| `ParallelExecutionEngine.Regeneration.cs` | Task regeneration with error feedback |
| `ParallelExecutionEngine.Utilities.cs` | Helper methods |

**Validation Modes:**
- **Individual** - Validate each task independently
- **Batch** - Validate all tasks together for cross-references
- **Hybrid** - Individual validation first, then batch for integration

## Execution Flow

```
User Request
    │
    ▼
┌───────────────────────────┐
│  AoTEngineOrchestrator    │
│  (Workflow Coordination)  │
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│  ParallelExecutionEngine  │
│  (Task Execution)         │
│                           │
│  ┌─────┐ ┌─────┐ ┌─────┐ │
│  │Task1│ │Task2│ │Task3│ │  ← Parallel execution
│  └──┬──┘ └──┬──┘ └──┬──┘ │
│     └───────┴───────┘    │
│              │           │
│              ▼           │
│        Validation        │
│              │           │
│              ▼           │
│     Summary Generation   │
└─────────────┬─────────────┘
              │
              ▼
         AoTResult
```

## Design Principles

- All `.cs` files are kept under 300 lines for maintainability
- Large classes use partial class pattern to split functionality
- Clear separation between orchestration, execution, and validation concerns
- Dependency injection used for service composition
