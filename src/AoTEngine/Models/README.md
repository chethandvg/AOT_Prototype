# Models Folder

This folder contains the data models and DTOs used throughout the AoT Engine.

## Components

### TaskNode
Represents an atomic task in the task decomposition graph.

**Files:**
- `TaskNode.cs` - Task node model with properties for dependencies, generated code, validation state

### ValidationResult
Result of code validation operations.

**Files:**
- `ValidationResult.cs` - Validation result with errors and warnings

### TaskDecomposition Models
Models for task decomposition requests and responses.

**Files:**
- `TaskDecompositionRequest.cs` - Request model for task decomposition
- `TaskDecompositionResponse.cs` - Response model with decomposed tasks

### Documentation Models
Models for documentation generation.

**Files:**
- `ProjectDocumentation.cs` - Complete project documentation
- `TaskSummaryRecord.cs` - Individual task summary record

### Type Registry and Symbol Table
Models for tracking types and symbols during code integration.

**Files:**
- `TypeRegistry.cs` - Registry for tracking types across tasks:
  - `TypeRegistryEntry` - Type definition metadata
  - `MemberSignature` - Member signature for conflict detection
  - `TypeConflict` - Conflict information
  - `TypeRegistry` - Central registry class
- `SymbolTable.cs` - Project-wide symbol tracking (ENHANCED):
  - `ProjectSymbolInfo` - Symbol information with FQN, namespace, kind
  - `SymbolTable` - Symbol lookup, tracking, and collision detection
  - `TypeDefinitionMetadata` - Metadata for structured output
  - `SymbolCollision` - Collision information between symbols (NEW)
  - `SymbolCollisionType` - Enum: DuplicateDefinition, AmbiguousName, MisplacedModel (NEW)
  - `GetSymbolsBySimpleName()` - Find types by simple name for ambiguity detection (NEW)
  - `IsAmbiguous()` - Check if a simple name is ambiguous (NEW)
  - `ValidateNamespaceConventions()` - Enforce DTOs in .Models namespace (NEW)
  - `GetSuggestedAlias()` - Generate using alias for ambiguous types (NEW)
  - `GenerateUsingAliases()` - Generate all using aliases for collisions (NEW)

### Contract Catalog Models (NEW)
Models for frozen API contracts in Contract-First generation.

**Files:**
- `ContractCatalog.cs` - Frozen contract definitions container:
  - `ContractCatalog` - Container with Enums, Interfaces, Models, AbstractClasses collections
    - `Freeze()` - Lock contracts after generation
    - `ContainsType()` - Check if type exists in contracts
    - `GetContract()` - Get contract by name
    - `GetAllContracts()` - Get all contracts
  - `EnumContract` - Enum definition with members and values
    - `Members` - List of enum members with optional values
    - `IsFlags` - Whether enum is a flags enum
    - `GenerateCode()` - Produces valid C# enum
  - `InterfaceContract` - Interface with methods, properties, type constraints
    - `Methods` - List of method signatures
    - `Properties` - List of property signatures
    - `BaseInterfaces` - Inherited interfaces
    - `TypeParameters` - Generic type parameters
    - `TypeConstraints` - Generic constraints
    - `GenerateCode()` - Produces valid C# interface
  - `ModelContract` - DTO/Model class definition
    - `Properties` - List of property definitions
    - `IsRecord` - Whether to generate record instead of class
    - `BaseClass` - Base class if any
    - `GenerateCode()` - Produces valid C# class/record
  - `AbstractClassContract` - Abstract class with abstract/virtual methods
    - `AbstractMethods` - Methods requiring override
    - `VirtualMethods` - Methods with default implementation
    - `IsSealed` - If true, generates sealed class (not abstract)
    - `GenerateCode()` - Produces valid C# abstract class
  - `MethodSignatureContract` - Method signature for interfaces/abstract classes
  - `PropertySignatureContract` - Property signature with getter/setter info
  - `ParameterContract` - Method parameter with name, type, default value

### Configuration Models
Configuration models for services.

**Files:**
- `AssemblyMappingConfig.cs` - Assembly mapping configuration

### Complexity Models
Models for task complexity analysis.

**Files:**
- `ComplexityMetrics.cs` - Complexity metrics for tasks

## Design Principles

- Models are simple data containers with minimal logic
- Each model file contains a single primary class
- All files are kept under 300 lines for maintainability
- Contract models include `GenerateCode()` for C# code generation
