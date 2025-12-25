# Core Folder

This folder contains the core orchestration and execution engine components for the AoT (Atom of Thought) Engine.

## Components

### AoTEngineOrchestrator
The main orchestrator that coordinates the entire AoT workflow including task decomposition, execution, validation, and documentation generation.

**Files:**
- `AoTEngineOrchestrator.cs` - Main orchestrator class (271 lines)

### AoTResult
Result model returned from AoT Engine execution.

**Files:**
- `AoTResult.cs` - Result and DocumentationPaths classes (80 lines)

### ParallelExecutionEngine
Engine for executing tasks in parallel based on their dependencies, with support for batch and hybrid validation modes.

**Files (partial classes):**
- `ParallelExecutionEngine.cs` - Core fields, constructor, complexity analysis (213 lines)
- `ParallelExecutionEngine.BatchValidation.cs` - Batch validation methods (145 lines)
- `ParallelExecutionEngine.HybridValidation.cs` - Hybrid validation methods (260 lines)
- `ParallelExecutionEngine.TaskExecution.cs` - Individual task execution (194 lines)
- `ParallelExecutionEngine.ProblemIdentification.cs` - Problem identification (167 lines)
- `ParallelExecutionEngine.Regeneration.cs` - Task regeneration methods (169 lines)
- `ParallelExecutionEngine.Utilities.cs` - Utility methods (144 lines)

### TaskComplexityAnalyzer
Analyzes task complexity to determine if decomposition is needed.

**Files:**
- `TaskComplexityAnalyzer.cs` - Complexity analysis logic

### CodeMergerService
Merges generated code snippets from multiple tasks.

**Files:**
- `CodeMergerService.cs` - Code merging logic

## Design Principles

- All `.cs` files are kept under 300 lines for maintainability
- Large classes use partial class pattern to split functionality
- Clear separation between orchestration, execution, and validation concerns
