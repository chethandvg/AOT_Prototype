# AoTEngine.Tests

This folder contains unit tests for the AoT Engine components.

## Test Files

### Core Tests
- `ParallelExecutionEngineTests.cs` - Tests for parallel task execution
- `TaskComplexityAnalyzerTests.cs` - Tests for complexity analysis and decomposition
- `TaskNodeTests.cs` - Tests for task node model

### Service Tests
- `CodeValidatorServiceTests.cs` - Tests for Roslyn-based code validation
- `CodeMergerServiceTests.cs` - Tests for code merging and integration
- `DocumentationServiceTests.cs` - Tests for documentation generation and export
- `ProjectBuildServiceTests.cs` - Tests for project creation and building
- `ProjectBuildServiceTests.Validation.cs` - Tests for build validation

### Integration Tests
- `IntegrationFixerTests.cs` - Tests for auto-fix capabilities
- `IntegrationCheckpointHandlerTests.cs` - Tests for checkpoint handling
- `CheckpointIntegrationTests.cs` - Integration tests for checkpointing
- `CheckpointServiceTests.cs` - Tests for checkpoint service

### Contract-First Tests (NEW)
- `ContractCatalogTests.cs` - Tests for ContractCatalog, EnumContract, InterfaceContract, ModelContract, AbstractClassContract:
  - Enum code generation with members and values
  - Interface code generation with methods, properties, type constraints
  - Model code generation (class and record)
  - Abstract class code generation
  - Sealed class generation (IsSealed=true generates sealed class, not abstract)
  - ContractCatalog freeze and lookup operations
  - JSON serialization/deserialization

- `PromptContextBuilderTests.cs` - Tests for PromptContextBuilder:
  - BuildCodeGenerationContext with contracts and guardrails
  - BuildInterfaceImplementationContext with method signatures
  - BuildEnumUsageContext with valid members
  - ValidateAgainstContracts for detecting violations

### Symbol Table Tests
- `TypeRegistryAndSymbolTableTests.cs` - Tests for TypeRegistry and SymbolTable:
  - Type registration and lookup
  - Conflict detection
  - Symbol collision detection (NEW)
  - Namespace validation (NEW)
  - Alias generation (NEW)

## Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test file
dotnet test --filter "FullyQualifiedName~ContractCatalogTests"

# Run Contract-First related tests
dotnet test --filter "FullyQualifiedName~ContractCatalog|FullyQualifiedName~PromptContextBuilder"
```

## Test Coverage

- **Total Tests**: 120
- **Core Tests**: ~25
- **Service Tests**: ~50
- **Integration Tests**: ~8
- **Contract-First Tests**: ~37 (NEW)

## Design Principles

- Tests follow Arrange-Act-Assert pattern
- Each test file corresponds to a source file
- Mock external dependencies (OpenAI API)
- Use meaningful test names describing behavior
