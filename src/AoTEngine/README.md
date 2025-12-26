# AoTEngine

The main library for the Atom of Thought (AoT) code generation engine.

## Overview

AoTEngine is an AI-powered code generation system that:
1. Decomposes complex coding tasks into atomic units
2. Generates and freezes API contracts before implementations (Contract-First)
3. Executes tasks in parallel based on dependencies
4. Validates generated code using Roslyn compilation
5. Auto-fixes common integration errors
6. Merges code with conflict resolution
7. Generates comprehensive documentation

## Folder Structure

```
AoTEngine/
├── Core/                        # Orchestration and execution engine
│   ├── AoTEngineOrchestrator.cs # Main workflow coordinator
│   ├── AoTResult.cs             # Execution result model
│   └── ParallelExecutionEngine*.cs
├── Models/                      # Data models and DTOs
│   ├── TaskNode.cs              # Atomic task representation
│   ├── ContractCatalog.cs       # Frozen API contracts
│   ├── SymbolTable.cs           # Project-wide symbol tracking
│   ├── TypeRegistry.cs          # Type conflict detection
│   └── ...
├── Services/                    # Service layer (organized by concern)
│   ├── AI/                      # OpenAI integration
│   │   ├── OpenAIService*.cs
│   │   ├── PromptContextBuilder.cs
│   │   └── KnownPackageVersions.cs
│   ├── Compilation/             # Project building, Roslyn
│   │   ├── ProjectBuildService*.cs
│   │   ├── AtomCompilationService.cs
│   │   └── AssemblyReferenceManager.cs
│   ├── Contracts/               # Contract-first generation
│   │   ├── ContractGenerationService.cs
│   │   └── ContractManifestService.cs
│   ├── Documentation/           # Docs export, checkpoints
│   │   ├── DocumentationService*.cs
│   │   └── CheckpointService.cs
│   ├── Integration/             # Code merging, auto-fix
│   │   ├── CodeMergerService*.cs
│   │   ├── IntegrationFixer.cs
│   │   ├── AutoFixService.cs
│   │   ├── TaskComplexityAnalyzer*.cs
│   │   ├── AutoDecomposer*.cs
│   │   └── UserInteractionService*.cs
│   └── Validation/              # Code validation
│       └── CodeValidatorService*.cs
├── appsettings.json             # Configuration
├── assembly-mappings.json       # Assembly reference mappings
└── Program.cs                   # Console entry point
```

## Key Features

### Contract-First Generation (NEW)
Generates and freezes API contracts before implementations to prevent:
- Ambiguous type references across namespaces
- Interface signature mismatches
- Missing enum members
- Sealed type inheritance violations

Enable with `enableContractFirst: true`:
```csharp
var result = await orchestrator.ExecuteAsync(
    userRequest,
    enableContractFirst: true,
    projectName: "MyProject"
);
```

### Task Decomposition
Breaks complex requests into atomic tasks with:
- Dependency tracking
- Parallel execution
- Automatic decomposition for tasks >300 lines

### Code Validation
Validates generated code using:
- Roslyn syntax analysis
- Roslyn compilation
- Per-atom compilation checks (NEW)
- Contract violation detection (NEW)

### Auto-Fix Capabilities
Automatically fixes common errors:
- Missing interface implementations
- Missing abstract method overrides
- Sealed type inheritance (converts to composition)
- Ambiguous type references (fully qualifies)

### Documentation Generation
Generates structured documentation:
- Per-task summaries
- Project-level architecture overview
- Markdown, JSON, JSONL exports
- Training data for fine-tuning

## Configuration

### appsettings.json
```json
{
  "OpenAI": {
    "ApiKey": "your-api-key",
    "Model": "gpt-4"
  },
  "Engine": {
    "MaxRetries": 3,
    "UseBatchValidation": true,
    "MaxLinesPerTask": 300,
    "EnableComplexityAnalysis": true,
    "EnableContractFirst": true
  }
}
```

## Dependencies

- .NET 9.0
- Microsoft.CodeAnalysis.CSharp (Roslyn)
- OpenAI (for AI code generation)
- System.Text.Json

## See Also

- [Core/README.md](Core/README.md) - Orchestration components
- [Models/README.md](Models/README.md) - Data models
- [Services/README.md](Services/README.md) - Service layer
- [../../ARCHITECTURE.md](../../ARCHITECTURE.md) - System architecture
- [../../USAGE.md](../../USAGE.md) - Usage guide
- [../../CHANGELOG.md](../../CHANGELOG.md) - Change history
