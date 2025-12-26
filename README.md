# AOT_Prototype
**Atom of Thought (AoT) Engine** - A C# implementation of an intelligent code generation system

## Overview

The AoT Engine is a sophisticated C# application that leverages OpenAI's GPT models to decompose complex programming tasks into atomic subtasks, execute them in parallel, validate the generated code, and merge the results into a complete solution. It also generates comprehensive documentation and training datasets.

## 📚 Documentation

- **[README.md](README.md)** - This file: Project overview and quick start guide
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Detailed system architecture and technical design
- **[MODULAR_ARCHITECTURE.md](MODULAR_ARCHITECTURE.md)** - Modular architecture guide and custom workflow examples
- **[USAGE.md](USAGE.md)** - Comprehensive usage examples and scenarios
- **[CHANGELOG.md](CHANGELOG.md)** - Complete feature reference and version history

## Features

### 1. **Task Decomposition**
- Uses OpenAI to decompose complex requests into atomic subtasks
- Creates a Directed Acyclic Graph (DAG) of dependencies
- Identifies parallel execution opportunities
- **Automatic complexity analysis**: Ensures each task generates ≤300 lines of code
- **Smart decomposition**: Complex tasks are automatically split into smaller subtasks

### 2. **Parallel Execution**
- Executes independent tasks concurrently using `async`/`await` and `Task.WhenAll`
- Respects task dependencies
- Feeds only required context to each task

### 3. **Code Validation**
- Compiles generated code snippets using Roslyn (Microsoft.CodeAnalysis)
- Runs linting checks for code quality
- Automatically retries with error feedback on validation failures

### 4. **Interactive Uncertainty Handling**
- Detects uncertainties in task descriptions
- Prompts user for clarification when needed
- Allows review and confirmation of task decomposition
- Supports user choices for ambiguous requirements

### 5. **Code Integration** ⚠️ ENHANCED
- Advanced Parse → Analyze → Fix → Emit pipeline
- **Type Registry**: Tracks types across tasks, detects duplicates
- **Symbol Table**: Maintains project-wide symbol information
- **Auto-fix**: Roslyn-based automatic fixing of common integration errors:
  - Duplicate type/interface/member definitions
  - Missing using statements  
  - Ambiguous type references
- **Manual Checkpoints**: Interactive conflict resolution for non-trivial cases
- Validates contracts between code snippets
- Merges all snippets into a cohesive solution
- Generates execution reports

### 6. **Contract-First Generation** ⚠️ NEW
- **Frozen Contracts**: Generate and freeze interfaces, enums, models, and abstract classes before implementations
- **Contract Catalog**: Central registry of all API surface contracts
- **Enum Governance**: Prevents invalid enum member references
- **Interface Compliance**: Ensures all interface methods are implemented with exact signatures
- **Abstract Method Enforcement**: Validates abstract method overrides match base class signatures
- **Sealed Type Detection**: Prevents inheritance from sealed classes, suggests composition
- **Ambiguity Prevention**: Detects and resolves type name collisions across namespaces
- **Contract Manifest**: Save/load contracts in JSON format for reproducibility

### 7. **Per-Atom Compilation Check** ⚠️ NEW
- Fast Roslyn compile after each file generation
- Classified diagnostics (SymbolCollision, MissingInterfaceMember, SignatureMismatch, etc.)
- Early failure detection before batch validation
- Auto-fix loop with compiler-driven patching

### 8. **Documentation Generation**
- Generates per-task summaries after code validation
- Synthesizes project-level architecture documentation
- Exports documentation in multiple formats:
  - **Markdown** (`Documentation.md`) - Human-readable documentation
  - **JSON** (`Documentation.json`) - Structured data for tooling
  - **JSONL** (`training_data.jsonl`) - Fine-tuning dataset for ML models
- Uses dependency summaries as context for better task coherence

### 7. **Incremental Checkpoint System**
- Automatically saves execution state after each task completion
- Creates both JSON and Markdown checkpoint files
- Enables progress tracking and execution recovery
- Includes:
  - All completed tasks with generated code
  - Project architecture summary
  - Dependency graph visualization
  - Validation status and attempts
- Saved to `checkpoints/` subdirectory in output directory
- Maintains `latest.json` and `latest.md` for easy access

## Architecture

> **🎯 Modular Design**: The AoT Engine has been refactored into **9 separate class library projects** for maximum reusability and flexibility. See **[MODULAR_ARCHITECTURE.md](MODULAR_ARCHITECTURE.md)** for details on creating custom workflows.

**Module Overview**:
```
src/
├── AoTEngine.Models/                    # Core data models and contracts
├── AoTEngine.Services.AI/               # OpenAI integration services
├── AoTEngine.Services.Compilation/      # Roslyn compilation services
├── AoTEngine.Services.Contracts/        # Contract-first generation
├── AoTEngine.Services.Documentation/    # Documentation generation
├── AoTEngine.Services.Integration/      # Code merging and integration
├── AoTEngine.Services.Validation/       # Code validation
├── AoTEngine.Core/                      # Core orchestration engine
└── AoTEngine/                           # Main executable (CLI)
```

**Key Components**:
- **Models**: TaskNode, ContractCatalog, SymbolTable, TypeRegistry, ValidationResult
- **Services.AI**: OpenAI integration, code generation, task decomposition
- **Services.Compilation**: Roslyn compilation, project building, assembly management
- **Services.Contracts**: Contract-first generation, frozen interfaces/enums
- **Services.Documentation**: Documentation export (Markdown, JSON, JSONL)
- **Services.Integration**: Code merging, auto-fix, complexity analysis
- **Services.Validation**: Code validation using Roslyn
- **Core**: Parallel execution engine, workflow orchestration

## Prerequisites

- .NET 9.0 SDK or later
- OpenAI API Key

## Installation

1. Clone the repository:
```bash
git clone https://github.com/chethandvg/AOT_Prototype.git
cd AOT_Prototype
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Configure OpenAI API Key:

**Option 1:** Update `appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "your-api-key-here",
    "Model": "gpt-5.1"
  }
}
```

**Option 2:** Set environment variable:
```bash
export OPENAI_API_KEY="your-api-key-here"
```

## Usage

### Running the Engine

```bash
cd src/AoTEngine
dotnet run
```

### Example Workflow

1. **Enter your request:**
```
Enter your coding request: Create a REST API for a todo list application with authentication
```

2. **Review task decomposition:**
The engine will decompose your request and ask for confirmation:
```
Task Decomposition Review
═══════════════════════════════════════════════════════════
Task task1: Create authentication service
  Dependencies: None

Task task2: Create todo item model
  Dependencies: None

Task task3: Create todo API controller
  Dependencies: task1, task2

Does this task decomposition look correct? (y/n):
```

3. **Handle uncertainties:**
If the engine detects vague requirements, it will ask for clarification:
```
⚠️  UNCERTAINTY DETECTED - User Input Required
═══════════════════════════════════════════════════════════
Context: Task ID: task1
Description: Create authentication service

Question: What security requirements are needed?
(authentication/authorization/encryption/validation)

Your response: JWT-based authentication with role-based authorization
```

4. **View results:**
The engine will execute tasks in parallel, validate code, and output:
- Execution report
- Final merged code (`generated_code.cs`)
- Documentation files (when output directory specified):
  - `Documentation.md`
  - `Documentation.json`
  - `training_data.jsonl`

## Building the Project

```bash
dotnet build
```

## Running Tests

```bash
dotnet test
```

## Configuration

### appsettings.json

```json
{
  "OpenAI": {
    "ApiKey": "YOUR_API_KEY",
    "Model": "gpt-5.1"
  },
  "Engine": {
    "MaxRetries": 3,
    "UseBatchValidation": true,
    "UseHybridValidation": true,
    "MaxLinesPerTask": 300,
    "EnableComplexityAnalysis": true,
    "EnableContractFirst": true
  },
  "Documentation": {
    "Enabled": true,
    "GeneratePerTask": true,
    "GenerateProjectSummary": true,
    "ExportMarkdown": true,
    "ExportJson": true,
    "ExportJsonl": true,
    "SummaryModel": "gpt-4o-mini",
    "MaxSummaryTokens": 300
  }
}

### Engine Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxLinesPerTask` | `300` | Maximum lines of code per generated task |
| `EnableComplexityAnalysis` | `true` | Enable automatic task complexity analysis and decomposition |
| `EnableContractFirst` | `false` | Enable Contract-First code generation (recommended for large projects) |
```

### Documentation Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Enable/disable documentation generation |
| `GeneratePerTask` | `true` | Generate summaries for each task |
| `GenerateProjectSummary` | `true` | Generate project-level architecture summary |
| `ExportMarkdown` | `true` | Export `Documentation.md` |
| `ExportJson` | `true` | Export `Documentation.json` |
| `ExportJsonl` | `true` | Export `training_data.jsonl` for fine-tuning |
| `SummaryModel` | `gpt-4o-mini` | Model used for generating summaries |
| `MaxSummaryTokens` | `300` | Maximum tokens for summary generation |

## How It Works

1. **Decomposition Phase**
   - User provides a high-level request
   - OpenAI decomposes it into atomic subtasks
   - Dependencies are identified
   - User reviews and confirms the decomposition
   - **Complexity analysis**: Tasks exceeding 300 lines are automatically split

2. **Uncertainty Resolution Phase**
   - System detects vague or ambiguous terms
   - User is prompted for clarification
   - Context is enriched with clarifications

3. **Contract Generation Phase** ⚠️ NEW (when enableContractFirst=true)
   - Analyzes all tasks to identify types needing contracts
   - Generates frozen contracts: enums, interfaces, models, abstract classes
   - Creates Contract Catalog with all API surface definitions
   - Saves contract manifest (contracts.json) for reproducibility
   - Registers all types in Symbol Table for collision detection

4. **Execution Phase**
   - Tasks are organized in topological order
   - Independent tasks execute in parallel
   - Each task receives contracts + only necessary context
   - Code is generated via OpenAI with contract-aware prompts
   - Per-atom compilation check validates each file immediately

5. **Validation Phase**
   - Generated code is compiled using Roslyn
   - Contract violations are detected (missing interface members, invalid enum usage, etc.)
   - Linting checks are performed
   - Auto-fix loop attempts to resolve common issues
   - If validation fails, re-prompt with errors
   - Retry up to MaxRetries times

6. **Documentation Phase**
   - Per-task summaries generated after validation
   - Summaries include purpose, key behaviors, edge cases
   - Validation notes track retry attempts
   - Dependency summaries used as context for coherence

7. **Integration Phase**
   - Parse → Analyze → Fix → Emit pipeline for robust integration
   - Type Registry tracks types across tasks to prevent duplicates
   - Automatic conflict detection and resolution strategies
   - Auto-fix for missing usings, duplicate definitions, ambiguous references
   - Manual checkpoint support for non-trivial conflicts
   - Code snippets are merged into cohesive solution
   - Final solution is compiled
   - Execution report is generated

8. **Documentation Export Phase**
   - Project-level documentation synthesized
   - Architecture summary generated
   - Documentation exported to multiple formats
   - Training dataset created for fine-tuning

## Output Files

When an output directory is specified, the engine generates:

| File | Description |
|------|-------------|
| `generated_code.cs` | Final merged C# code |
| `ExecutionReport.txt` | Execution statistics and task details |
| `Documentation.md` | Human-readable project documentation |
| `Documentation.json` | Structured JSON documentation |
| `training_data.jsonl` | Fine-tuning dataset (one line per task) |
| `contracts.json` | Contract manifest (when Contract-First enabled) |
| `<Namespace>/<Type>.cs` | Individual contract files (when Contract-First enabled) |

### Training Dataset Format (JSONL)

Each line in `training_data.jsonl` contains:
```json
{
  "instruction": "Generate C# code for: Create authentication service",
  "input": {
    "task_description": "Create authentication service",
    "dependencies": [],
    "expected_types": ["AuthService", "IAuthService"],
    "namespace": "MyProject.Services"
  },
  "output": "// Generated C# code...",
  "metadata": {
    "task_id": "task1",
    "purpose": "Implements JWT authentication",
    "key_behaviors": ["Token generation", "Token validation"],
    "validation_notes": "Passed on first attempt"
  }
}
```

## Key Technologies

- **C# / .NET 9.0**: Core framework
- **OpenAI SDK**: GPT integration for code generation
- **Roslyn (Microsoft.CodeAnalysis)**: Code compilation and validation
- **Newtonsoft.Json**: JSON serialization
- **xUnit**: Unit testing framework

## Project Structure

```
AOT_Prototype/
├── src/
│   ├── AoTEngine.Models/                 # Core data models and contracts
│   │   ├── TaskNode.cs
│   │   ├── ContractCatalog.cs
│   │   ├── SymbolTable.cs
│   │   └── ...
│   ├── AoTEngine.Services.AI/            # OpenAI integration
│   │   ├── OpenAIService*.cs
│   │   └── PromptContextBuilder.cs
│   ├── AoTEngine.Services.Compilation/   # Roslyn compilation
│   │   ├── ProjectBuildService*.cs
│   │   └── AtomCompilationService.cs
│   ├── AoTEngine.Services.Contracts/     # Contract-first generation
│   │   ├── ContractGenerationService.cs
│   │   └── ContractManifestService.cs
│   ├── AoTEngine.Services.Documentation/ # Documentation export
│   │   ├── DocumentationService*.cs
│   │   └── CheckpointService.cs
│   ├── AoTEngine.Services.Integration/   # Code merging
│   │   ├── CodeMergerService*.cs
│   │   └── IntegrationFixer.cs
│   ├── AoTEngine.Services.Validation/    # Code validation
│   │   └── CodeValidatorService*.cs
│   ├── AoTEngine.Core/                   # Orchestration
│   │   ├── AoTEngineOrchestrator.cs
│   │   └── ParallelExecutionEngine*.cs
│   └── AoTEngine/                        # Main executable
│       ├── Program.cs
│       └── appsettings.json
├── tests/
│   └── AoTEngine.Tests/                  # Unit tests
├── ARCHITECTURE.md                       # System architecture
├── MODULAR_ARCHITECTURE.md               # Modular design guide
├── USAGE.md                              # Usage examples
├── CHANGELOG.md                          # Version history
├── Directory.Build.props                 # Common project settings
└── AoTEngine.sln                         # Solution file
```

### Module Organization

| Module | Purpose | Key Dependencies |
|--------|---------|------------------|
| `AoTEngine.Models` | Core data models and contracts | Roslyn |
| `AoTEngine.Services.AI` | OpenAI API integration, code generation | Models, OpenAI SDK |
| `AoTEngine.Services.Compilation` | Roslyn compilation, project building | Models, AI, Roslyn |
| `AoTEngine.Services.Contracts` | Contract-first generation | Models, OpenAI SDK |
| `AoTEngine.Services.Documentation` | Documentation export, checkpoints | Models, AI |
| `AoTEngine.Services.Integration` | Code merging, conflict resolution | Models, AI, Validation |
| `AoTEngine.Services.Validation` | Code validation using Roslyn | Models, Compilation |
| `AoTEngine.Core` | Orchestration engine | All Services |
| `AoTEngine` | Main executable (CLI) | Core + All Services |

> See **[MODULAR_ARCHITECTURE.md](MODULAR_ARCHITECTURE.md)** for detailed module documentation and custom workflow examples.

## Example Output

```
=== AoT Engine Execution Report ===

Total Tasks: 4
Completed Tasks: 4
Validated Tasks: 4

Task Details:
  - task1: Create authentication service
    Dependencies: None
    Status: ✓ Completed, ✓ Validated
  - task2: Create todo item model
    Dependencies: None
    Status: ✓ Completed, ✓ Validated
  - task3: Create todo API controller
    Dependencies: task1, task2
    Status: ✓ Completed, ✓ Validated
  - task4: Create unit tests
    Dependencies: task1, task2, task3
    Status: ✓ Completed, ✓ Validated

Merged Code Length: 3521 characters
Merged Code Lines: 142

✅ Documentation synthesis complete.
   📄 Exported Markdown documentation to: ./output/Documentation.md
   📄 Exported JSON documentation to: ./output/Documentation.json
   📄 Exported JSONL training dataset to: ./output/training_data.jsonl
```

## Limitations

- Requires valid OpenAI API key
- Code generation quality depends on OpenAI model
- Complex enterprise patterns may need manual refinement
- Test execution is simulated (actual test running TBD)

## Future Enhancements

- [x] Documentation generation layer
- [x] Fine-tuning dataset export
- [x] **Task complexity analysis** - Automatic detection of complex tasks
- [x] **Automatic decomposition** - Split complex tasks into ≤300 line subtasks
- [x] **Contract-First generation** - Freeze API contracts before implementations
- [x] **Per-atom compilation** - Validate each file immediately after generation
- [x] **Auto-fix loop** - Compiler-driven patching for common errors
- [ ] Actual unit test execution
- [ ] Support for multiple programming languages
- [ ] Integration with CI/CD pipelines
- [ ] Advanced dependency resolution
- [ ] Code optimization suggestions
- [ ] Support for custom code templates
- [ ] Integration with version control systems

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues.

## License

See LICENSE file for details.

## Acknowledgments

Built with inspiration from the "Atom of Thought" concept, emphasizing decomposition of complex problems into atomic, manageable units.

