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

## Recent Updates

### OpenAI Responses API Integration (gpt-5.1-codex)
The code generation system has been upgraded to use OpenAI's Responses API (`/v1/responses`) with **response chaining** for improved context continuity:

**Key Features:**
- **Response Storage**: All code generation responses are stored server-side for retrieval
- **Response Chaining**: Subsequent tasks reference previous responses via `previous_response_id`
- **Context Continuity**: Maintains conversation context across dependent tasks
- **Extended Timeout**: 300-second timeout for complex code generation requests

**Benefits:**
- Dependent tasks automatically inherit context from their dependencies
- Error corrections maintain awareness of original generation intent
- Reduced type mismatches across generated code
- Better coherence in multi-file projects

See [Services/AI/README.md](Services/AI/README.md) for detailed API documentation.

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
│   ├── AI/                      # OpenAI integration (gpt-5.1-codex with chaining)
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

### Contract-First Generation
Generates and freezes API contracts before implementations to prevent:
- Ambiguous type references across namespaces
- Interface signature mismatches
- Missing enum members
- Sealed type inheritance violations

**With Response Chaining:**
Contract-aware generation now leverages response chaining to maintain contract context across dependent implementations, further reducing signature mismatches.

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
- Response chaining for context-aware generation

### Code Validation
Validates generated code using:
- Roslyn syntax analysis
- Roslyn compilation
- Per-atom compilation checks
- Contract violation detection

### Auto-Fix Capabilities
Automatically fixes common errors:
- Missing interface implementations
- Missing abstract method overrides
- Sealed type inheritance (converts to composition)
- Ambiguous type references (fully qualifies)

**With Response Chaining:**
Error correction maintains the original generation context, allowing for more surgical fixes that preserve working code.

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
    "Model": "gpt-5.1"
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

**Note:** The code generation model `gpt-5.1-codex` is configured internally for optimal code generation with response chaining.

## Architecture Highlights

### Response Chain Flow
```
Task Dependency Graph:
  task1 (no deps)
    ↓
  [Generate] → resp_1
    ↓
  task2 (depends on task1)
    ↓
  [Generate with previous_response_id: resp_1] → resp_2
    ↓
  task3 (depends on task2)
    ↓
  [Generate with previous_response_id: resp_2] → resp_3
```

If validation errors occur:
```
  task2 validation errors
    ↓
  [Regenerate with previous_response_id: resp_2] → resp_2_fixed
    ↓
  Update chain: task2 → resp_2_fixed
```

This ensures every code generation operation has maximum context awareness.

## Dependencies

- .NET 9.0
- Microsoft.CodeAnalysis.CSharp (Roslyn)
- OpenAI (for AI code generation with gpt-5.1-codex)
- System.Text.Json
- Newtonsoft.Json

## See Also

- [Core/README.md](Core/README.md) - Orchestration components
- [Models/README.md](Models/README.md) - Data models
- [Services/README.md](Services/README.md) - Service layer overview
- [Services/AI/README.md](Services/AI/README.md) - OpenAI Responses API details
- [../../ARCHITECTURE.md](../../ARCHITECTURE.md) - System architecture
- [../../USAGE.md](../../USAGE.md) - Usage guide
- [../../CHANGELOG.md](../../CHANGELOG.md) - Change history
