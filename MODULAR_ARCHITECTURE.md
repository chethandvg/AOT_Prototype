# Modular Architecture Guide

## Overview

The AoT Engine has been refactored from a monolithic structure into a modular architecture consisting of 9 separate class library projects. This modularization enables:

- **Reusability**: Individual modules can be reused across different workflows
- **Maintainability**: Smaller, focused projects are easier to understand and maintain
- **Testability**: Each module can be tested independently
- **Flexibility**: Different workflows can mix and match only the modules they need
- **Clear Separation of Concerns**: Each module has a well-defined responsibility

## Project Structure

```
AOT_Prototype/
├── src/
│   ├── AoTEngine.Models/                    # Core data models and contracts
│   ├── AoTEngine.Services.AI/               # OpenAI integration services
│   ├── AoTEngine.Services.Compilation/      # Roslyn compilation services
│   ├── AoTEngine.Services.Contracts/        # Contract-first generation
│   ├── AoTEngine.Services.Documentation/    # Documentation generation
│   ├── AoTEngine.Services.Integration/      # Code merging and integration
│   ├── AoTEngine.Services.Validation/       # Code validation
│   ├── AoTEngine.Core/                      # Core orchestration engine
│   └── AoTEngine/                           # Main executable (CLI)
├── tests/
│   └── AoTEngine.Tests/                     # Unit tests
└── AoTEngine.sln                            # Solution file
```

## Module Details

### 1. AoTEngine.Models
**Namespace**: `AoTEngine.Models`  
**Dependencies**: 
- Microsoft.CodeAnalysis.CSharp (4.11.0)

**Purpose**: Core data models, DTOs, and contracts used throughout the system.

**Key Classes**:
- `TaskNode` - Represents an atomic task in the DAG
- `TaskSummaryRecord` - Structured task documentation
- `ProjectDocumentation` - Project-level documentation
- `CheckpointData` - Checkpoint snapshot structure
- `ComplexityMetrics` - Task complexity analysis metrics
- `TaskDecompositionStrategy` - Decomposition strategies
- `TypeRegistry` - Type tracking & conflict detection
- `SymbolTable` - Project-wide symbol information
- `ContractCatalog` - Frozen contract definitions
- `ValidationResult` - Validation results

**Usage**: This is the foundational module. Almost all other modules depend on it.

---

### 2. AoTEngine.Services.AI
**Namespace**: `AoTEngine.Services`  
**Dependencies**:
- AoTEngine.Models
- OpenAI (2.8.0)
- Newtonsoft.Json (13.0.4)
- Microsoft.CodeAnalysis.CSharp (4.11.0)
- Microsoft.Extensions.Logging.Abstractions (9.0.0)
- Microsoft.Extensions.Http (9.0.0)

**Purpose**: OpenAI API integration for task decomposition, code generation, and documentation.

**Key Classes**:
- `OpenAIService` - Main service for OpenAI interactions (partial class)
  - `OpenAIService.CodeGeneration.cs` - Code generation methods
  - `OpenAIService.ContractAware.cs` - Contract-aware generation
  - `OpenAIService.ContractExtraction.cs` - Contract extraction
  - `OpenAIService.Documentation.cs` - Documentation generation
  - `OpenAIService.PackageVersions.cs` - Package version queries
  - `OpenAIService.Prompts.cs` - Prompt templates
  - `OpenAIService.TaskDecomposition.cs` - Task decomposition
- `PromptContextBuilder` - Enhanced prompt context building
- `KnownPackageVersions` - Package version registry

**Usage**: Required for any workflow that needs AI-powered code generation or task decomposition.

---

### 3. AoTEngine.Services.Compilation
**Namespace**: `AoTEngine.Services`  
**Dependencies**:
- AoTEngine.Models
- AoTEngine.Services.AI
- Microsoft.CodeAnalysis.CSharp (4.11.0)
- Microsoft.Extensions.Configuration.Abstractions (9.0.0)
- Microsoft.Extensions.Logging.Abstractions (9.0.0)

**Purpose**: Roslyn-based compilation, project building, and assembly management.

**Key Classes**:
- `ProjectBuildService` - Project creation and building (partial class)
  - `ProjectBuildService.BuildValidation.cs` - Build validation
  - `ProjectBuildService.FileOperations.cs` - File operations
  - `ProjectBuildService.PackageManagement.cs` - Package management
- `AtomCompilationService` - Per-file Roslyn compilation
- `AssemblyReferenceManager` - Assembly resolution and management

**Usage**: Required for workflows that need to compile generated code or manage .NET projects.

---

### 4. AoTEngine.Services.Contracts
**Namespace**: `AoTEngine.Services`  
**Dependencies**:
- AoTEngine.Models
- OpenAI (2.8.0)
- Newtonsoft.Json (13.0.4)
- Microsoft.Extensions.Logging.Abstractions (9.0.0)

**Purpose**: Contract-first code generation - freezing interfaces, enums, models before implementation.

**Key Classes**:
- `ContractGenerationService` - Generate frozen contracts
- `ContractManifestService` - Save/load contract manifests

**Usage**: Required for contract-first workflows where API surfaces are defined upfront.

---

### 5. AoTEngine.Services.Documentation
**Namespace**: `AoTEngine.Services`  
**Dependencies**:
- AoTEngine.Models
- AoTEngine.Services.AI
- Newtonsoft.Json (13.0.4)
- Microsoft.Extensions.Logging.Abstractions (9.0.0)

**Purpose**: Generate structured documentation and training datasets.

**Key Classes**:
- `DocumentationService` - Main documentation service (partial class)
  - `DocumentationService.Export.cs` - Export to various formats
  - `DocumentationService.Markdown.cs` - Markdown generation
  - `DocumentationService.Utilities.cs` - Utility methods
- `CheckpointService` - Checkpoint management

**Usage**: Required for workflows that need to generate documentation or training data.

---

### 6. AoTEngine.Services.Integration
**Namespace**: `AoTEngine.Services`  
**Dependencies**:
- AoTEngine.Models
- AoTEngine.Services.AI
- AoTEngine.Services.Validation
- Microsoft.CodeAnalysis.CSharp (4.11.0)
- Microsoft.Extensions.Logging.Abstractions (9.0.0)

**Purpose**: Code merging, conflict resolution, and integration services.

**Key Classes**:
- `CodeMergerService` - Code merging (partial class)
  - `CodeMergerService.Integration.cs` - Advanced integration
- `IntegrationFixer` - Roslyn-based auto-fix
- `IntegrationCheckpointHandler` - Conflict resolution checkpoints
- `AutoFixService` - Auto-fix loop service
- `AutoDecomposer` - Automatic task decomposition (partial class)
- `TaskComplexityAnalyzer` - Complexity analysis (partial class)
- `UserInteractionService` - User interaction (partial class)

**Usage**: Required for workflows that merge multiple code snippets or need conflict resolution.

---

### 7. AoTEngine.Services.Validation
**Namespace**: `AoTEngine.Services`  
**Dependencies**:
- AoTEngine.Models
- AoTEngine.Services.Compilation
- Microsoft.CodeAnalysis.CSharp (4.11.0)
- Microsoft.Extensions.Configuration.Abstractions (9.0.0)
- Microsoft.Extensions.Logging.Abstractions (9.0.0)

**Purpose**: Code validation using Roslyn compiler.

**Key Classes**:
- `CodeValidatorService` - Main validation service (partial class)
  - `CodeValidatorService.Compilation.cs` - Compilation validation
  - `CodeValidatorService.Integration.cs` - Integration validation

**Usage**: Required for workflows that need to validate generated code.

---

### 8. AoTEngine.Core
**Namespace**: `AoTEngine.Core`  
**Dependencies**:
- AoTEngine.Models
- AoTEngine.Services.AI
- AoTEngine.Services.Compilation
- AoTEngine.Services.Contracts
- AoTEngine.Services.Documentation
- AoTEngine.Services.Integration
- AoTEngine.Services.Validation
- Newtonsoft.Json (13.0.4)
- Microsoft.Extensions.Logging.Abstractions (9.0.0)

**Purpose**: Core orchestration engine - coordinates all services to execute workflows.

**Key Classes**:
- `AoTEngineOrchestrator` - Main workflow orchestrator
- `ParallelExecutionEngine` - Parallel task execution (partial class)
  - `ParallelExecutionEngine.BatchValidation.cs` - Batch validation
  - `ParallelExecutionEngine.HybridValidation.cs` - Hybrid validation
  - `ParallelExecutionEngine.ProblemIdentification.cs` - Problem identification
  - `ParallelExecutionEngine.Regeneration.cs` - Code regeneration
  - `ParallelExecutionEngine.TaskExecution.cs` - Task execution
  - `ParallelExecutionEngine.Utilities.cs` - Utility methods
- `AoTResult` - Execution result model

**Usage**: This is the orchestration layer that coordinates all services. Required for the standard AoT workflow.

---

### 9. AoTEngine (Main Executable)
**Namespace**: `AoTEngine`  
**Dependencies**:
- All the above modules
- Microsoft.Extensions.Configuration (9.0.0)
- Microsoft.Extensions.Configuration.Json (9.0.0)
- Microsoft.Extensions.Configuration.EnvironmentVariables (9.0.0)

**Purpose**: Command-line interface and entry point for the AoT Engine.

**Key Files**:
- `Program.cs` - Entry point
- `appsettings.json` - Configuration
- `assembly-mappings.json` - Assembly mappings

**Usage**: This is the executable that users run. It depends on all other modules.

---

## Dependency Graph

```
AoTEngine (Executable)
    └── AoTEngine.Core
        ├── AoTEngine.Models
        ├── AoTEngine.Services.AI
        │   └── AoTEngine.Models
        ├── AoTEngine.Services.Compilation
        │   ├── AoTEngine.Models
        │   └── AoTEngine.Services.AI
        ├── AoTEngine.Services.Contracts
        │   └── AoTEngine.Models
        ├── AoTEngine.Services.Documentation
        │   ├── AoTEngine.Models
        │   └── AoTEngine.Services.AI
        ├── AoTEngine.Services.Integration
        │   ├── AoTEngine.Models
        │   ├── AoTEngine.Services.AI
        │   └── AoTEngine.Services.Validation
        └── AoTEngine.Services.Validation
            ├── AoTEngine.Models
            └── AoTEngine.Services.Compilation
```

## Creating Custom Workflows

The modular architecture allows you to create custom workflows by selecting only the modules you need. Here are some examples:

### Example 1: Simple Code Generator (No Validation)

If you want a simple code generator that doesn't validate code:

```csharp
// Reference only these modules:
// - AoTEngine.Models
// - AoTEngine.Services.AI

var openAI = new OpenAIService(apiKey);
var request = new TaskDecompositionRequest { /* ... */ };
var response = await openAI.DecomposeTaskAsync(request);

foreach (var task in response.Tasks)
{
    var code = await openAI.GenerateCodeAsync(task);
    // Do something with code
}
```

### Example 2: Documentation Generator

If you want to generate documentation from existing code:

```csharp
// Reference only these modules:
// - AoTEngine.Models
// - AoTEngine.Services.AI
// - AoTEngine.Services.Documentation

var openAI = new OpenAIService(apiKey);
var docService = new DocumentationService(openAI);

var documentation = await docService.SynthesizeProjectDocumentationAsync(tasks);
await docService.ExportMarkdownAsync(documentation, outputPath);
```

### Example 3: Code Validator Only

If you want to validate existing code:

```csharp
// Reference only these modules:
// - AoTEngine.Models
// - AoTEngine.Services.Compilation
// - AoTEngine.Services.Validation

var validator = new CodeValidatorService();
var result = await validator.ValidateCodeAsync(code);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine(error);
    }
}
```

### Example 4: Contract-First Workflow

If you want to use contract-first generation:

```csharp
// Reference these modules:
// - AoTEngine.Models
// - AoTEngine.Services.AI
// - AoTEngine.Services.Contracts
// - AoTEngine.Services.Compilation
// - AoTEngine.Services.Validation

var contractService = new ContractGenerationService(apiKey);
var catalog = await contractService.GenerateContractCatalogAsync(tasks);

var manifestService = new ContractManifestService();
await manifestService.SaveManifestAsync(catalog, outputPath);

// Then generate implementations against these frozen contracts
var openAI = new OpenAIService(apiKey);
var code = await openAI.GenerateCodeWithContractsAsync(task, catalog);
```

## Benefits of Modular Architecture

1. **Smaller Deployments**: Only deploy the modules you need
2. **Easier Testing**: Test each module independently
3. **Better Separation**: Clear boundaries between different concerns
4. **Reusability**: Use the same modules in different workflows
5. **Maintainability**: Smaller codebases are easier to maintain
6. **Flexibility**: Mix and match modules as needed
7. **Versioning**: Version modules independently
8. **Team Collaboration**: Different teams can own different modules

## Building and Testing

### Build All Projects
```bash
dotnet build
```

### Build Specific Module
```bash
cd src/AoTEngine.Services.AI
dotnet build
```

### Run All Tests
```bash
dotnet test
```

### Package a Module
```bash
cd src/AoTEngine.Services.AI
dotnet pack
```

## Migration Guide

If you have existing code that referenced the monolithic `AoTEngine` project, the migration is straightforward:

**Before (Monolithic)**:
```csharp
using AoTEngine.Services;
using AoTEngine.Models;
using AoTEngine.Core;
```

**After (Modular)**:
```csharp
using AoTEngine.Services;  // Still works - all service modules use this namespace
using AoTEngine.Models;     // Still works
using AoTEngine.Core;       // Still works
```

The namespaces have been preserved, so most code should work without changes. You just need to update your project references to include the specific module projects instead of the monolithic one.

## Future Enhancements

With this modular architecture, future enhancements could include:

1. **NuGet Packages**: Publish each module as a separate NuGet package
2. **Plugin Architecture**: Allow third-party modules to extend functionality
3. **Alternative Implementations**: Provide alternative implementations of services (e.g., different AI providers)
4. **Language Support**: Add modules for other programming languages
5. **Integration Modules**: Create modules for integrating with other systems (GitHub, Azure DevOps, etc.)

## Conclusion

The modular architecture provides a solid foundation for building various workflows while maintaining code reusability and clarity. Each module has a single, well-defined responsibility and can be used independently or in combination with other modules.
