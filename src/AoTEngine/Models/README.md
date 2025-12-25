# Models Folder

This folder contains the data models and DTOs used throughout the AoT Engine.

## Components

### TaskNode
Represents an atomic task in the task decomposition graph.

**Files:**
- `TaskNode.cs` - Task node model with properties for dependencies, generated code, validation state

### ValidationResult
Result of code validation operations.

**Files:**
- `ValidationResult.cs` - Validation result with errors and warnings

### TaskDecomposition Models
Models for task decomposition requests and responses.

**Files:**
- `TaskDecompositionRequest.cs` - Request model for task decomposition
- `TaskDecompositionResponse.cs` - Response model with decomposed tasks

### Documentation Models
Models for documentation generation.

**Files:**
- `ProjectDocumentation.cs` - Complete project documentation
- `TaskSummaryRecord.cs` - Individual task summary record

### Configuration Models
Configuration models for services.

**Files:**
- `AssemblyMappingConfig.cs` - Assembly mapping configuration

### Complexity Models
Models for task complexity analysis.

**Files:**
- `ComplexityMetrics.cs` - Complexity metrics for tasks

## Design Principles

- Models are simple data containers with minimal logic
- Each model file contains a single primary class
- All files are kept under 300 lines for maintainability
