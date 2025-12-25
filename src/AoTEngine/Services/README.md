# Services Folder

This folder contains the service layer components for the AoT Engine, handling AI interaction, code validation, project building, and documentation generation.

## Components

### OpenAIService
Handles all interactions with OpenAI API for task decomposition, code generation, and documentation.

**Files (partial classes):**
- `OpenAIService.cs` - Core fields, constructor, task decomposition (131 lines)
- `OpenAIService.CodeGeneration.cs` - Code generation and regeneration (221 lines)
- `OpenAIService.Prompts.cs` - Prompt generation methods (241 lines)
- `OpenAIService.ContractExtraction.cs` - Type contract extraction (116 lines)
- `OpenAIService.PackageVersions.cs` - Package version queries (181 lines)
- `OpenAIService.Documentation.cs` - Documentation generation (212 lines)
- `OpenAIService.TaskDecomposition.cs` - Complex task decomposition (159 lines)

### CodeValidatorService
Validates generated C# code using Roslyn compilation.

**Files (partial classes):**
- `CodeValidatorService.cs` - Core fields, constructor, main validation (103 lines)
- `CodeValidatorService.Compilation.cs` - Compilation and assembly resolution (293 lines)
- `CodeValidatorService.Integration.cs` - Integration validation and linting (216 lines)

### CodeMergerService
Merges code snippets from multiple tasks with advanced integration capabilities.

**Files (partial classes):**
- `CodeMergerService.cs` - Basic merge operations and contract validation
- `CodeMergerService.Integration.cs` - Advanced Parse → Analyze → Fix → Emit pipeline:
  - Type Registry for tracking types across tasks
  - Symbol Table for project-wide symbol tracking
  - Deduplication with multiple resolution strategies
  - Auto-fix for common integration errors

### IntegrationFixer
Provides Roslyn-based auto-fix capabilities for common integration errors.

**Files:**
- `IntegrationFixer.cs` - Auto-fix for:
  - Duplicate type/interface/member definitions
  - Missing using statements
  - Ambiguous type references
  - Type mismatches

### IntegrationCheckpointHandler
Handles manual checkpoints for non-trivial conflicts during code integration.

**Files:**
- `IntegrationCheckpointHandler.cs` - Conflict reporting and user interaction

### ProjectBuildService
Creates and builds .NET projects from generated code.

**Files (partial classes):**
- `ProjectBuildService.cs` - Core fields, constructors, CreateProjectFromTasksAsync (272 lines)
- `ProjectBuildService.PackageManagement.cs` - Package extraction and management (190 lines)
- `ProjectBuildService.FileOperations.cs` - File saving and entry point creation (203 lines)
- `ProjectBuildService.BuildValidation.cs` - Build and restore methods (291 lines)

### DocumentationService
Generates and exports documentation for tasks and projects.

**Files (partial classes):**
- `DocumentationService.cs` - Core fields, constructor, summary generation (229 lines)
- `DocumentationService.Export.cs` - Export methods (JSON, Markdown, JSONL) (118 lines)
- `DocumentationService.Markdown.cs` - Markdown generation (183 lines)
- `DocumentationService.Utilities.cs` - Utility methods (169 lines)

### UserInteractionService
Handles user interactions for clarifications and confirmations.

**Files (partial classes):**
- `UserInteractionService.cs` - Prompt and clarification methods (263 lines)
- `UserInteractionService.UncertaintyDetection.cs` - Uncertainty detection logic (61 lines)

### AssemblyReferenceManager
Manages assembly references for Roslyn compilation.

**Files:**
- `AssemblyReferenceManager.cs` - Assembly reference management

### KnownPackageVersions
Provides known stable package versions for .NET 9.

**Files:**
- `KnownPackageVersions.cs` - Package version mappings

## Design Principles

- All `.cs` files are kept under 300 lines for maintainability
- Large classes use partial class pattern to split functionality
- Clear separation of concerns between different service responsibilities
