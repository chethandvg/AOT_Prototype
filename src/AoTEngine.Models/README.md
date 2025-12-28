# Models Layer

This folder contains the data models and DTOs used throughout the AoT Engine.

## Overview

| Category | Files | Purpose |
|----------|-------|---------|
| **Task Execution** | `TaskNode.cs`, `ValidationResult.cs` | Core task and validation models |
| **Decomposition** | `TaskDecompositionRequest.cs`, `TaskDecompositionResponse.cs`, `TaskDecompositionStrategy.cs` | Task decomposition workflow |
| **Documentation** | `ProjectDocumentation.cs`, `TaskSummaryRecord.cs`, `CheckpointData.cs` | Documentation and checkpoints |
| **Type Tracking** | `TypeRegistry.cs`, `SymbolTable.cs` | Symbol and type management |
| **Contracts** | `ContractCatalog.cs` | Contract-first generation |
| **Complexity** | `ComplexityMetrics.cs` | Task complexity analysis |

## Components

### Task Execution Models

**TaskNode** (`TaskNode.cs`)
- Represents an atomic task in the task decomposition graph
- Properties: Id, Description, Dependencies, GeneratedCode, ValidationStatus
- Tracks completion status, retry count, validation errors

**ValidationResult** (`ValidationResult.cs`)
- Result of code validation operations
- Properties: IsValid, Errors, Warnings

### Decomposition Models

**TaskDecompositionRequest** (`TaskDecompositionRequest.cs`)
- Request model for task decomposition
- Properties: OriginalRequest, Context

**TaskDecompositionResponse** (`TaskDecompositionResponse.cs`)
- Response model with decomposed tasks
- Properties: Description, Tasks

**TaskDecompositionStrategy** (`TaskDecompositionStrategy.cs`)
- Decomposition strategy for complex tasks
- Types: Functional, PartialClass, InterfaceBased, LayerBased

### Documentation Models

**ProjectDocumentation** (`ProjectDocumentation.cs`)
- Complete project documentation container
- Properties: ProjectRequest, Description, TaskRecords, ModuleIndex

**TaskSummaryRecord** (`TaskSummaryRecord.cs`)
- Structured summary for each task
- Properties: TaskId, Purpose, KeyBehaviors, EdgeCases, ValidationNotes

**CheckpointData** (`CheckpointData.cs`)
- Checkpoint snapshot structure for progress tracking

### Type Tracking Models

**TypeRegistry** (`TypeRegistry.cs`)
- Central registry for tracking types across tasks
- Detects duplicate type definitions and member conflicts
- Resolution strategies: KeepFirst, MergeAsPartial, RemoveDuplicate, FailFast

**SymbolTable** (`SymbolTable.cs`)
- Project-wide symbol tracking with collision detection
- Methods: `GetSymbolsBySimpleName()`, `IsAmbiguous()`, `ValidateNamespaceConventions()`

### Contract Catalog Models

**ContractCatalog** (`ContractCatalog.cs`)
- Frozen contract definitions container with:
  - `EnumContract` - Enum definitions with members
  - `InterfaceContract` - Interface with methods, properties
  - `ModelContract` - DTO/Model class definitions
  - `AbstractClassContract` - Abstract class with abstract/virtual methods
  - Each contract includes `GenerateCode()` for C# source generation

### Complexity Models

**ComplexityMetrics** (`ComplexityMetrics.cs`)
- Task complexity analysis results
- Properties: EstimatedLineCount, ComplexityScore, RequiresDecomposition

## Design Principles

- Models are simple data containers with minimal logic
- Each model file contains a single primary class
- All files are kept under 300 lines for maintainability
- Contract models include `GenerateCode()` for C# code generation
- Immutability preferred where appropriate
