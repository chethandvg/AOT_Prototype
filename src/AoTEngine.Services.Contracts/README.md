# Contract Services

This folder contains services for Contract-First code generation, where API contracts (interfaces, enums, models, abstract classes) are generated and frozen before implementation code.

## Components

### ContractGenerationService

Generates and freezes API contracts from task decomposition analysis.

**Key Methods:**
- `GenerateContractCatalogAsync()` - Main entry point for contract generation
- `GenerateEnumContractsAsync()` - Extracts enum definitions from tasks
- `GenerateInterfaceContractsAsync()` - Extracts interface definitions with method signatures
- `GenerateModelContractsAsync()` - Extracts DTO/model definitions
- `GenerateAbstractClassContractsAsync()` - Extracts abstract class definitions

**Workflow:**
1. Analyzes task decomposition for types needing contracts
2. Identifies types mentioned in multiple tasks
3. Generates frozen contracts with exact signatures
4. Registers contracts in Symbol Table for collision detection
5. Returns immutable `ContractCatalog`

### ContractManifestService

Persists and loads frozen contracts in JSON format for reproducibility.

**Key Methods:**
- `SaveManifestAsync()` - Serializes `ContractCatalog` to `contracts.json`
- `LoadManifestAsync()` - Loads manifest and returns `ContractCatalog`
- `GenerateContractFilesAsync()` - Creates `.cs` files for each contract
- `ValidateEnumMember()` - Validates enum member exists in contract
- `ValidateInterfaceMethod()` - Validates method signature matches contract
- `IsTypeSealed()` - Checks if type is sealed in contracts

**Manifest Format:**
```json
{
  "frozenAtUtc": "2024-01-15T10:30:00Z",
  "enums": [...],
  "interfaces": [...],
  "models": [...],
  "abstractClasses": [...]
}
```

## Benefits of Contract-First Generation

1. **Consistency** - All implementations reference the same frozen contracts
2. **Parallel Development** - Tasks can be executed independently with guaranteed interface compatibility
3. **Early Error Detection** - Contract violations are caught during generation, not integration
4. **Reproducibility** - Contracts can be saved and reloaded for incremental code generation

## Design Principles

- Contracts are immutable once frozen
- All contract types include `GenerateCode()` for C# source generation
- Services validate contracts before and after code generation
