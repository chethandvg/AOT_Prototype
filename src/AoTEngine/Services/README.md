# Services Layer

This folder contains the service layer components for the AoT Engine, organized into logical subfolders based on their responsibilities.

## Folder Structure

```
Services/
├── AI/                 # OpenAI API integration and prompt building
├── Compilation/        # Project building, assembly management, Roslyn compilation
├── Contracts/          # Contract-first generation and manifest management
├── Documentation/      # Documentation generation and export, checkpointing
├── Integration/        # Code merging, conflict resolution, complexity analysis
└── Validation/         # Code validation using Roslyn
```

## Subfolder Overview

| Folder | Responsibility | Key Components |
|--------|----------------|----------------|
| **[AI](./AI/)** | OpenAI API integration, prompts, code generation | `OpenAIService`, `PromptContextBuilder` |
| **[Compilation](./Compilation/)** | .NET project creation, build, assembly resolution | `ProjectBuildService`, `AtomCompilationService` |
| **[Contracts](./Contracts/)** | Contract-first code generation | `ContractGenerationService`, `ContractManifestService` |
| **[Documentation](./Documentation/)** | Docs generation, export, checkpoints | `DocumentationService`, `CheckpointService` |
| **[Integration](./Integration/)** | Code merging, auto-fix, decomposition | `CodeMergerService`, `IntegrationFixer`, `AutoDecomposer` |
| **[Validation](./Validation/)** | Code compilation and validation | `CodeValidatorService` |

## Service Dependencies

```
┌─────────────┐     ┌─────────────────┐
│  AI/        │────▶│  Validation/    │
│  OpenAI     │     │  CodeValidator  │
└──────┬──────┘     └────────┬────────┘
       │                     │
       ▼                     ▼
┌─────────────┐     ┌─────────────────┐
│ Contracts/  │────▶│  Compilation/   │
│ Generation  │     │  ProjectBuild   │
└──────┬──────┘     └────────┬────────┘
       │                     │
       ▼                     ▼
┌─────────────┐     ┌─────────────────┐
│Integration/ │────▶│ Documentation/  │
│ Merging     │     │ Export          │
└─────────────┘     └─────────────────┘
```

## Design Principles

- **Separation of Concerns**: Each subfolder handles a distinct aspect of the system
- **300-Line Limit**: All `.cs` files are kept under 300 lines for maintainability
- **Partial Classes**: Large classes are split across multiple files for readability
- **Stateless Services**: Services are stateless where possible, with configuration injected via constructor
- **Interface Abstractions**: Core services implement interfaces for testability

## Quick Navigation

- **Need to modify AI prompts?** → See [AI/](./AI/)
- **Build/compilation issues?** → See [Compilation/](./Compilation/)
- **Contract-first generation?** → See [Contracts/](./Contracts/)
- **Documentation export?** → See [Documentation/](./Documentation/)
- **Code merging/conflicts?** → See [Integration/](./Integration/)
- **Validation errors?** → See [Validation/](./Validation/)

Each subfolder contains its own `README.md` with detailed documentation of its components.
