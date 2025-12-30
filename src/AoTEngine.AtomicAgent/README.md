# Atomic Thought Framework - Autonomous C# Coding Agent

This is a new startup project that implements the **Atomic Thought Framework** as described in the architectural blueprint. It represents a paradigm shift from linear "Chat-based" code generation to a graph-based, dependency-managed approach for autonomous software development.

## Overview

The Atomic Agent is an intelligent orchestrator that integrates:
- **The Brain**: OpenAI Assistant SDK for cognitive functions
- **The Workspace**: Sandboxed local file system
- **The Compiler**: .NET Roslyn for deterministic validation

## 8 Core Components

### 1. **Workspace Service**
- Sandboxed file system access to prevent accidental modification of critical files
- Project scaffolding using `dotnet` CLI
- Path validation and security boundaries

### 2. **Blackboard Service** (Shared Knowledge Base)
- Implements the `solution_manifest.json` schema
- Tracks structural, temporal, and semantic dimensions
- Maintains project hierarchy (Core, Infrastructure, Presentation layers)
- Semantic Symbol Table for high-fidelity type lookups

### 3. **Planner Agent**
- **Abstractions First Strategy**: Defines interfaces before implementations
- **Topological Sort** using Kahn's Algorithm for dependency resolution
- Circular dependency detection and automatic refactoring
- Layer validation (enforces Clean Architecture rules)

### 4. **Clarification Loop**
- Ambiguity detection using vague term analysis
- Interactive user prompts for missing specifications
- Hot/Cold memory lookup (future enhancement)

### 5. **Context Engine**
- **Tiered Context Injection**:
  - **Tier 1 (Global)**: Project metadata, architecture layers, completed files
  - **Tier 2 (Local)**: Semantic signatures of dependencies (not full source)
  - **Tier 3 (Target)**: Specific atom requirements
- **Hot Cache**: In-memory caching for fast retrieval
- Token-efficient context building

### 6. **Atomic Worker Agent**
- Executes individual "atoms" (units of work)
- Code generation via OpenAI with context-aware prompts
- Markdown extraction for clean code output
- Retry loop with error feedback

### 7. **Roslyn Feedback Loop**
- In-memory compilation using `CSharpCompilation`
- Diagnostic capture and formatting
- **Semantic Extraction**: Populates Symbol Table from compiled code
- Self-correcting loop: compilation errors → LLM feedback → retry

### 8. **Token Garbage Collection**
- (Planned) Summarization atoms for long-running sessions
- Thread reset with project conscience preservation

## Architecture Highlights

### Abstractions First
All interfaces and DTOs are defined before implementations, preventing:
- Circular dependencies
- "Hallucination" of non-existent dependencies
- Architectural violations (e.g., Core depending on Infrastructure)

### Dependency Graph (DAG)
```
DTOs (no deps) → Interfaces (depend on DTOs) → Implementations (depend on both)
```

### Clean Architecture Enforcement
The blackboard validates that:
- Core layer has **zero** dependencies
- Infrastructure depends **only** on Core
- Presentation depends on Core and Infrastructure

## Configuration

### `appsettings.json`
```json
{
  "OpenAI": {
    "ApiKey": "",
    "Model": "gpt-4"
  },
  "Workspace": {
    "RootPath": "./output",
    "EnableSandboxing": true
  },
  "Planner": {
    "AbstractionsFirst": true,
    "EnableTopologicalSort": true
  },
  "Context": {
    "EnableTieredInjection": true,
    "EnableHotCache": true
  }
}
```

### `.env` File
```bash
OPENAI_API_KEY=your-api-key-here
```

## Usage

```bash
cd src/AoTEngine.AtomicAgent
dotnet run
```

### Example Request
```
Create a simple user management system with a User DTO, 
IUserRepository interface, and FileUserRepository implementation 
that stores users in a JSON file.
```

### Output
The system will:
1. Analyze for ambiguities and request clarification
2. Generate a plan with atoms in dependency order
3. Execute atoms: generate code → compile → validate → retry if needed
4. Save all files to the workspace
5. Create `solution_manifest.json` with project state

## Differences from Existing AoTEngine

| Feature | AoTEngine | AtomicAgent |
|---------|-----------|-------------|
| Architecture | Task-based parallel execution | DAG-based atomic execution |
| Planning | OpenAI decomposition | Abstractions First + Topological Sort |
| Context | Full code injection | Tiered (Global/Local/Target) signatures |
| Validation | Batch + Hybrid | Per-atom with Roslyn feedback |
| State Management | In-memory TaskNodes | Persistent Blackboard (JSON) |
| Scaffolding | Project creation service | Workspace with dotnet CLI |

## Reused Components

The AtomicAgent **reuses** these existing modules:
- `AoTEngine.Models` - TaskNode, ValidationResult
- `AoTEngine.Services.AI` - OpenAIService for LLM interaction
- `AoTEngine.Services.Validation` - CodeValidatorService
- `AoTEngine.Services.Compilation` - ProjectBuildService

## Future Enhancements

- [ ] Implement Token Garbage Collection (Component 8)
- [ ] OpenAI Assistant SDK integration (instead of ChatClient)
- [ ] Thread persistence for multi-session projects
- [ ] Vector Store integration for RAG
- [ ] Parallel atom execution (currently sequential)
- [ ] Integration with existing ContractGenerationService
- [ ] Checkpoint system for recovery

## Technical Stack

- **.NET 9.0** - Latest runtime features
- **Microsoft.Extensions.Hosting** - Generic Host for DI and lifecycle
- **Microsoft.CodeAnalysis (Roslyn)** - In-memory compilation
- **OpenAI SDK** - LLM integration
- **Newtonsoft.Json** - Manifest serialization
- **dotenv.net** - Environment variable management

## Key Insights from the Blueprint

> "By treating code generation as the construction of a Directed Acyclic Graph (DAG) of atoms, rather than a continuous stream of text, the system can enforce architectural rigor."

> "The Semantic Symbol Table (SST) is a critical innovation for handling limited context windows. Instead of feeding the LLM entire source code, inject concise signatures."

> "Roslyn transforms compilation errors from show-stopping failures into actionable feedback prompts, creating a self-correcting autonomous loop."

## License

See the main repository LICENSE file.
