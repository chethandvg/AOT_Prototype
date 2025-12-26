# Validation Services

This folder contains services responsible for validating generated C# code using Roslyn compilation and integration checks.

## Components

### CodeValidatorService (Partial Class)

Validates generated C# code using Roslyn compilation.

| File | Responsibility |
|------|----------------|
| `CodeValidatorService.cs` | Core fields, constructor, main validation entry points |
| `CodeValidatorService.Compilation.cs` | Compilation logic and assembly resolution |
| `CodeValidatorService.Integration.cs` | Integration validation and linting checks |

**Key Methods:**
- `ValidateCodeAsync()` - Main validation entry point
- `CompileCode()` - Roslyn-based compilation with diagnostics
- `ValidateIntegration()` - Cross-task reference validation
- `LintCode()` - Code quality and style checks

**Validation Pipeline:**
1. **Syntax Check** - Parse code into Roslyn syntax tree
2. **Semantic Analysis** - Resolve symbols and check types
3. **Compilation** - Full compilation with all references
4. **Linting** - Code quality and naming convention checks

**Error Handling:**
- Compilation errors are captured and returned in `ValidationResult`
- Warnings are separated from errors for optional display
- Detailed error messages include line numbers and suggestions

## Integration with Other Services

| Service | Integration |
|---------|-------------|
| `OpenAIService` | Provides error feedback for code regeneration |
| `CodeMergerService` | Validates merged code before final output |
| `AtomCompilationService` | Per-atom validation during generation |
| `IntegrationFixer` | Provides fix suggestions for common errors |

## Configuration

The `CodeValidatorService` can be configured via `appsettings.json`:

```json
{
  "Engine": {
    "MaxRetries": 3,
    "UseBatchValidation": true,
    "UseHybridValidation": true
  }
}
```

## Design Principles

- All files are kept under 300 lines for maintainability
- Large classes use partial class pattern to split functionality
- Validation is deterministic and repeatable
- Error messages are designed for both human and LLM consumption
