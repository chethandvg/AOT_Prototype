# AoTEngine.Tests

Unit tests for the AoT Engine components.

## Overview

This project contains comprehensive tests for all AoT Engine components, organized by the service categories they test.

## Test Categories

### Core Layer Tests

| Test File | Tests For |
|-----------|-----------|
| `ParallelExecutionEngineTests.cs` | Parallel task execution engine |
| `TaskNodeTests.cs` | Task node model |

### AI Service Tests

| Test File | Tests For |
|-----------|-----------|
| `PromptContextBuilderTests.cs` | Prompt context building with contracts |

### Compilation Service Tests

| Test File | Tests For |
|-----------|-----------|
| `ProjectBuildServiceTests.cs` | Project creation and building |
| `ProjectBuildServiceTests.Validation.cs` | Build validation |

### Contract Service Tests

| Test File | Tests For |
|-----------|-----------|
| `ContractCatalogTests.cs` | Contract catalog, enum/interface/model contracts |

### Documentation Service Tests

| Test File | Tests For |
|-----------|-----------|
| `DocumentationServiceTests.cs` | Documentation generation and export |
| `CheckpointServiceTests.cs` | Checkpoint service |
| `CheckpointIntegrationTests.cs` | Checkpoint integration tests |

### Integration Service Tests

| Test File | Tests For |
|-----------|-----------|
| `CodeMergerServiceTests.cs` | Code merging and integration |
| `IntegrationFixerTests.cs` | Auto-fix capabilities |
| `IntegrationCheckpointHandlerTests.cs` | Checkpoint handling |
| `TaskComplexityAnalyzerTests.cs` | Complexity analysis |
| `RegenerationSuggestionsTests.cs` | Task regeneration |

### Validation Service Tests

| Test File | Tests For |
|-----------|-----------|
| `CodeValidatorServiceTests.cs` | Roslyn-based code validation |

### Model Tests

| Test File | Tests For |
|-----------|-----------|
| `TypeRegistryAndSymbolTableTests.cs` | Type registry and symbol table |

## Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test file
dotnet test --filter "FullyQualifiedName~ContractCatalogTests"

# Run tests by category
dotnet test --filter "FullyQualifiedName~Integration"
dotnet test --filter "FullyQualifiedName~Contract"
```

## Test Coverage

- **Total Tests**: 131
- **Core Tests**: ~25
- **Service Tests**: ~60
- **Integration Tests**: ~10
- **Contract-First Tests**: ~36

## Design Principles

- Tests follow Arrange-Act-Assert pattern
- Each test file corresponds to a source file
- Mock external dependencies (OpenAI API)
- Use meaningful test names describing behavior
- Tests are deterministic and repeatable
