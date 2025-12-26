# Documentation Services

This folder contains services for generating, synthesizing, and exporting project documentation and checkpoints.

## Components

### DocumentationService (Partial Class)

Generates and exports documentation for tasks and projects.

| File | Responsibility |
|------|----------------|
| `DocumentationService.cs` | Core fields, constructor, summary generation |
| `DocumentationService.Export.cs` | Export methods (JSON, Markdown, JSONL) |
| `DocumentationService.Markdown.cs` | Markdown generation and formatting |
| `DocumentationService.Utilities.cs` | Utility methods for documentation |

**Key Methods:**
- `GenerateTaskSummaryAsync()` - Generates summary for a single task
- `SynthesizeProjectDocumentationAsync()` - Creates complete project documentation
- `ExportMarkdownAsync()` - Exports human-readable Markdown
- `ExportJsonAsync()` - Exports structured JSON
- `ExportJsonlDatasetAsync()` - Exports fine-tuning dataset

**Export Formats:**

| Format | File | Purpose |
|--------|------|---------|
| Markdown | `Documentation.md` | Human-readable documentation |
| JSON | `Documentation.json` | Structured data for tooling |
| JSONL | `training_data.jsonl` | Fine-tuning dataset for ML models |

### CheckpointService

Manages incremental checkpoints during code generation for progress tracking and recovery.

**Key Features:**
- Automatically saves execution state after each task completion
- Creates both JSON and Markdown checkpoint files
- Enables progress tracking and execution recovery
- Maintains `latest.json` and `latest.md` for easy access

**Checkpoint Contents:**
- All completed tasks with generated code
- Project architecture summary
- Dependency graph visualization
- Validation status and attempts

**Checkpoint Location:** `checkpoints/` subdirectory in output directory

## Training Dataset Format

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

## Design Principles

- Documentation generation is non-blocking and failure-tolerant
- All files are kept under 300 lines for maintainability
- Large classes use partial class pattern
- File I/O errors are handled gracefully without failing the main workflow
