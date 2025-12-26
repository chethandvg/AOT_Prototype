# AoT Engine Architecture

## System Overview

The AoT (Atom of Thought) Engine is designed around seven core principles:
1. **Decomposition**: Break complex tasks into atomic units
2. **Parallelism**: Execute independent tasks concurrently
3. **Validation**: Ensure code quality through compilation and linting
4. **Interaction**: Engage users when uncertainties arise
5. **Documentation**: Generate structured documentation and training data
6. **Complexity Management**: Ensure tasks stay within manageable size limits (≤300 lines)
7. **Contract-First**: Generate and freeze API contracts before implementations

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         User Request                             │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                   AoTEngineOrchestrator                          │
│  (Main workflow coordinator)                                     │
└─────┬───────────────────┬───────────────────┬────────────────────┘
      │                   │                   │
      │ 1. Decompose      │ 2. Execute       │ 3. Merge & Validate
      ▼                   ▼                   ▼
┌─────────────┐  ┌──────────────────┐  ┌────────────────┐
│  OpenAI     │  │   Parallel       │  │  CodeMerger    │
│  Service    │  │   Execution      │  │  Service       │
│             │  │   Engine         │  │                │
│ • Decompose │  │ • DAG Analysis   │  │ • Merge Code   │
│ • Generate  │  │ • Task.WhenAll   │  │ • Validate     │
│ • Re-prompt │  │ • Dependency     │  │   Contracts    │
│ • Summary   │  │   Resolution     │  │ • Generate     │
│   Generation│  │ • Summary Gen    │  │   Report       │
└─────────────┘  └──────┬───────────┘  └────────────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  Contract           │
              │  Generation (NEW)   │
              │                     │
              │ • Freeze Enums      │
              │ • Freeze Interfaces │
              │ • Freeze Models     │
              │ • Freeze Abstract   │
              │ • Contract Catalog  │
              └─────────┬───────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  Code Validator     │
              │  Service            │
              │                     │
              │ • Roslyn Compiler   │
              │ • Syntax Check      │
              │ • Linting           │
              │ • Retry Logic       │
              └─────────┬───────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  Atom Compilation   │
              │  Service (NEW)      │
              │                     │
              │ • Per-file compile  │
              │ • Diagnostic class  │
              │ • Contract validate │
              │ • Early failure     │
              └─────────┬───────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  Auto-Fix           │
              │  Service (NEW)      │
              │                     │
              │ • Interface impl    │
              │ • Abstract override │
              │ • Sealed→Compose    │
              │ • Ambiguous refs    │
              └─────────┬───────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  User Interaction   │
              │  Service            │
              │                     │
              │ • Detect            │
              │   Uncertainties     │
              │ • Prompt User       │
              │ • Collect Input     │
              │ • Review Tasks      │
              └─────────────────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  Documentation      │
              │  Service            │
              │                     │
              │ • Task Summaries    │
              │ • Project Docs      │
              │ • Export Markdown   │
              │ • Export JSON       │
              │ • Export JSONL      │
              └─────────────────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  Task Complexity    │
              │  Analyzer           │
              │                     │
              │ • Line Count Est    │
              │ • Complexity Score  │
              │ • Type/Method Count │
              │ • Auto-decompose    │
              │ • Max 300 lines     │
              └─────────────────────┘
```

## Component Details

### 1. Models Layer

**TaskNode**
- Represents an atomic task in the DAG
- Properties: Id, Description, Dependencies, GeneratedCode, ValidationStatus
- Summary, SummaryModel, ValidationAttemptCount, SummaryGeneratedAtUtc
- Tracks: Completion status, retry count, validation errors

**ComplexityMetrics**
- Task complexity analysis results
- Properties: EstimatedLineCount, ExpectedTypeCount, DependencyCount
- EstimatedMethodCount, ComplexityScore (0-100), RequiresDecomposition
- RecommendedSubtaskCount, MaxLineThreshold (default: 300)
- Used to determine if task needs splitting

**TaskDecompositionStrategy**
- Decomposition strategy for complex tasks
- Types: Functional, PartialClass, InterfaceBased, LayerBased
- Properties: OriginalTaskId, Subtasks, PartialClassConfig
- Manages shared state and dependencies between subtasks

**TypeRegistry** (NEW)
- Central registry for tracking types across tasks
- Detects duplicate type definitions and member conflicts
- Properties: Types (dictionary), Conflicts (list)
- Resolution strategies: KeepFirst, MergeAsPartial, RemoveDuplicate, FailFast
- Prevents "namespace already contains definition" errors

**TypeRegistryEntry** (NEW)
- Metadata for registered types
- Properties: TypeName, Namespace, FullyQualifiedName, Kind
- OwnerTaskId, IsPartial, Members, SyntaxNode
- Tracks type ownership for conflict resolution

**MemberSignature** (NEW)
- Signature information for class members
- Properties: Name, Kind (Constructor/Method/Property/Field), ParameterTypes
- SignatureKey for conflict detection
- Prevents duplicate member definitions

**TypeConflict** (NEW)
- Information about detected type conflicts
- Properties: FullyQualifiedName, ConflictType, SuggestedResolution
- ExistingEntry, NewEntry, ConflictingMembers
- Used by IntegrationCheckpointHandler for resolution

**SymbolTable** (ENHANCED)
- Project-wide symbol tracking with collision detection
- Properties: Symbols (dictionary), Collisions (list)
- Generates "Known Types" blocks for prompt injection
- Helps prevent model from redefining existing symbols
- `GetSymbolsBySimpleName()`, `IsAmbiguous()`, `ValidateNamespaceConventions()`
- `GetSuggestedAlias()`, `GenerateUsingAliases()`

**ContractCatalog** (NEW)
- Frozen contract definitions container
- Properties: Enums, Interfaces, Models, AbstractClasses
- `Freeze()` to lock contracts after generation
- `ContainsType()`, `GetContract()`, `GetAllContracts()`
- Each contract has `GenerateCode()` for C# generation

**EnumContract** (NEW)
- Enum definition with members and values
- Properties: Name, Namespace, Members, IsFlags
- `GenerateCode()` produces valid C# enum

**InterfaceContract** (NEW)
- Interface with methods, properties, type constraints
- Properties: Name, Methods, Properties, BaseInterfaces
- TypeParameters, TypeConstraints for generics
- `GenerateCode()` produces valid C# interface

**ModelContract** (NEW)
- DTO/Model class definition
- Properties: Name, Properties, IsRecord, BaseClass
- `GenerateCode()` produces valid C# class/record

**AbstractClassContract** (NEW)
- Abstract class with abstract/virtual methods
- Properties: Name, AbstractMethods, VirtualMethods, IsSealed
- Note: IsSealed=true generates sealed class (not abstract)
- `GenerateCode()` produces valid C# abstract class

**SymbolCollision** (NEW)
- Collision information between symbols
- Properties: SimpleName, ExistingSymbol, NewSymbol, CollisionType

**SymbolCollisionType** (NEW)
- Enum: DuplicateDefinition, AmbiguousName, MisplacedModel

**ProjectSymbolInfo** (ENHANCED)
- Symbol information for known types
- Properties: FullyQualifiedName, Namespace, Name, Kind
- DefinedByTaskId, Signature
- Used for context injection into subsequent prompts

**TaskSummaryRecord**
- Structured summary for each task
- Properties: TaskId, TaskDescription, Purpose, KeyBehaviors, EdgeCases
- ValidationNotes, GeneratedCodeHash, SummaryModel, CreatedUtc
- Used for documentation and training data export

**ProjectDocumentation**
- Project-level documentation container
- Properties: ProjectRequest, Description, TaskRecords, ModuleIndex
- HighLevelArchitectureSummary, DependencyGraphSummary
- Generated after all tasks complete

**TaskDecompositionRequest/Response**
- Request: Original user request + context
- Response: List of TaskNodes with dependencies

**ValidationResult**
- IsValid: Boolean success flag
- Errors: List of compilation/validation errors
- Warnings: List of non-critical issues

### 2. Services Layer

**OpenAIService**
```csharp
// General tasks use OpenAI SDK (ChatClient) with gpt-5.1
DecomposeTaskAsync()
  ├─> Sends structured prompt to GPT via ChatClient
  ├─> Parses JSON response into TaskNodes
  └─> Returns TaskDecompositionResponse

// Code generation uses HttpClient with gpt-5.1-codex for direct API control
GenerateCodeAsync()
  ├─> Builds context from dependencies
  ├─> Calls CallCodeGenChatCompletionAsync() via HttpClient
  ├─> Sends code generation prompt to gpt-5.1-codex
  └─> Returns C# code snippet

GenerateCodeWithContractsAsync() // NEW - Contract-aware generation
  ├─> Uses frozen contracts for context
  ├─> Injects exact interface/abstract signatures
  ├─> Calls CallCodeGenChatCompletionAsync() via HttpClient
  ├─> Validates against contracts
  └─> Returns contract-compliant C# code

RegenerateCodeWithErrorsAsync()
  ├─> Includes validation errors in prompt
  ├─> Calls CallCodeGenChatCompletionAsync() via HttpClient
  ├─> Requests corrected code from gpt-5.1-codex
  └─> Returns improved snippet

CallCodeGenChatCompletionAsync() // NEW - Direct HTTP API call
  ├─> Validates messages collection
  ├─> Constructs JSON request body with strongly-typed DTOs
  ├─> Uses static shared HttpClient to avoid socket exhaustion
  ├─> Sends POST to https://api.openai.com/v1/chat/completions
  ├─> Parses response with ChatCompletionResponse DTO
  ├─> Extracts detailed error information on failure
  └─> Returns generated code or throws HttpRequestException

GenerateTaskSummaryAsync()
  ├─> Analyzes generated code
  ├─> Generates structured JSON summary via ChatClient
  └─> Returns TaskSummaryInfo

GenerateArchitectureSummaryAsync()
  ├─> Analyzes all task summaries
  ├─> Generates high-level overview via ChatClient
  └─> Returns markdown summary

DecomposeComplexTaskAsync()
  ├─> Receives task estimated to exceed 300 lines
  ├─> Creates subtask breakdown with OpenAI via ChatClient
  ├─> Manages dependencies between subtasks
  └─> Returns list of smaller TaskNodes
```

**ContractGenerationService** (NEW)
```csharp
GenerateContractCatalogAsync()
  ├─> Analyzes task decomposition
  ├─> Identifies types needing contracts
  ├─> Generates enums, interfaces, models
  ├─> Registers in Symbol Table
  └─> Returns frozen ContractCatalog

GenerateEnumContractsAsync()
  ├─> Extracts enum definitions from tasks
  └─> Returns list of EnumContract

GenerateInterfaceContractsAsync()
  ├─> Extracts interface definitions
  ├─> Includes method signatures
  └─> Returns list of InterfaceContract
```

**ContractManifestService** (NEW)
```csharp
SaveManifestAsync()
  ├─> Serializes ContractCatalog to JSON
  └─> Saves to contracts.json

LoadManifestAsync()
  ├─> Loads JSON manifest
  └─> Returns ContractCatalog

GenerateContractFilesAsync()
  ├─> Creates .cs files for each contract
  └─> Returns list of generated file paths

ValidateEnumMember()
  ├─> Checks if enum member exists
  └─> Returns true/false

ValidateInterfaceMethod()
  ├─> Validates method signature matches
  └─> Returns MethodValidationResult

IsTypeSealed()
  └─> Checks if type is sealed in contracts
```

**PromptContextBuilder** (NEW)
```csharp
BuildCodeGenerationContext()
  ├─> Injects task details
  ├─> Adds frozen contract signatures
  ├─> Adds known symbols block
  ├─> Adds guardrails (DO NOT redefine)
  └─> Returns enhanced prompt context

BuildInterfaceImplementationContext()
  ├─> Lists required methods with exact signatures
  └─> Returns implementation requirements

BuildAbstractClassImplementationContext()
  ├─> Lists abstract methods to override
  ├─> Warns if sealed (use composition)
  └─> Returns override requirements

BuildEnumUsageContext()
  ├─> Lists valid enum members
  └─> Returns enum usage requirements

ValidateAgainstContracts()
  ├─> Checks for redefined types
  └─> Returns list of violations
```

**AtomCompilationService** (NEW)
```csharp
CompileAtom()
  ├─> Fast Roslyn compile per file
  ├─> Classifies diagnostics:
  │   ├─> SymbolCollision
  │   ├─> MissingInterfaceMember
  │   ├─> SignatureMismatch
  │   ├─> MissingEnumMember
  │   ├─> SealedInheritance
  │   └─> Other
  └─> Returns AtomCompilationResult

ValidateAgainstContracts()
  ├─> Checks for contract violations
  └─> Returns list of ContractViolation

GenerateCompilationSummary()
  └─> Human-readable error summary for LLM
```

**AutoFixService** (NEW)
```csharp
TryAutoFixAsync()
  ├─> Attempts to fix compilation errors
  ├─> Retry loop (max 3 attempts)
  └─> Returns AutoFixResult

TryFixMissingInterfaceMember()
  ├─> Adds stub implementation
  └─> Returns (success, newCode)

TryFixMissingAbstractMember()
  ├─> Adds override stub
  └─> Returns (success, newCode)

TryFixSealedInheritance()
  ├─> Converts inheritance to composition
  ├─> Adds private field for sealed type
  └─> Returns (success, newCode)

TryFixAmbiguousReference()
  ├─> Fully qualifies type name
  └─> Returns (success, newCode)
```

**TaskComplexityAnalyzer**
```csharp
AnalyzeTask()
  ├─> Estimates line count from task description
  ├─> Calculates type and method complexity
  ├─> Computes complexity score (0-100)
  ├─> Determines if decomposition needed (>300 lines)
  └─> Returns ComplexityMetrics

AnalyzeTasksForDecomposition()
  ├─> Analyzes all tasks in batch
  ├─> Filters tasks needing decomposition
  └─> Returns list of complex tasks
```

**AutoDecomposer** (NEW)
```csharp
DecomposeComplexTaskAsync()
  ├─> Determines best decomposition strategy
  ├─> Calls OpenAI for intelligent splitting
  ├─> Validates subtasks for circular dependencies
  ├─> Sets up partial class configuration if needed
  └─> Returns TaskDecompositionStrategy

DetermineDecompositionType()
  ├─> Functional: Multiple independent types
  ├─> PartialClass: Single large class
  ├─> InterfaceBased: Interface + implementation
  └─> LayerBased: Multi-layer service
```

**CodeValidatorService**
```csharp
ValidateCodeAsync()
  ├─> Parses code with Roslyn
  ├─> Checks syntax errors
  ├─> Attempts compilation
  └─> Returns ValidationResult

LintCode()
  ├─> Checks code quality rules
  ├─> Verifies naming conventions
  └─> Returns warnings
```

**CodeMergerService** (ENHANCED)
```csharp
MergeCodeSnippetsAsync()
  ├─> Extracts using statements
  ├─> Organizes by namespace
  ├─> Merges into cohesive solution
  └─> Validates merged code

MergeWithIntegrationAsync() // NEW - Advanced integration pipeline
  ├─> Parse all code snippets into syntax trees
  ├─> Build Type Registry and detect conflicts
  ├─> Apply conflict resolution strategies:
  │   ├─> KeepFirst - Keep first definition, discard duplicates
  │   ├─> MergeAsPartial - Convert to partial classes
  │   ├─> RemoveDuplicate - Remove conflicting members
  │   └─> FailFast - Fail for unresolvable conflicts
  ├─> Apply auto-fixes using Roslyn
  ├─> Emit deduplicated merged code
  └─> Return MergeResult with conflicts and applied fixes

ValidateContracts()
  ├─> Checks dependency satisfaction
  ├─> Validates all tasks completed
  └─> Returns contract validation result

CreateExecutionReport()
  └─> Generates summary statistics
```

**IntegrationFixer** (NEW)
```csharp
TryFix()
  ├─> Analyzes Roslyn diagnostics
  ├─> Classifies errors into fixable buckets
  ├─> Applies deterministic auto-fixes:
  │   ├─> CS0246: Missing type → Add using statement
  │   ├─> CS0234: Missing namespace → Add using statement
  │   ├─> CS0104: Ambiguous reference → Fully qualify type
  │   ├─> CS0111: Duplicate member → Remove duplicate
  │   └─> CS0101: Duplicate type → Remove duplicate
  └─> Returns IntegrationFixResult

AddMissingUsings()
  ├─> Parses code with Roslyn
  ├─> Identifies existing usings
  ├─> Adds missing namespace imports
  └─> Returns updated code

RemoveDuplicateTypes()
  ├─> Finds duplicate type declarations
  ├─> Removes based on conflict resolution
  └─> Returns deduplicated code

ConvertToPartialClasses()
  ├─> Identifies classes to convert
  ├─> Adds partial modifier
  └─> Returns updated code
```

**IntegrationCheckpointHandler** (NEW)
```csharp
GenerateConflictReports()
  ├─> Creates detailed conflict reports
  ├─> Includes declaration diffs
  ├─> Lists available resolution options
  └─> Returns list of ConflictReport

PromptForResolutionAsync()
  ├─> Displays conflict reports to user
  ├─> Prompts for resolution choice
  ├─> Collects user selections
  └─> Returns CheckpointResult with resolutions

RequiresManualIntervention()
  └─> Determines if conflicts need human review
```

**DocumentationService**
```csharp
GenerateTaskSummaryAsync()
  ├─> Validates input (task, dependencies)
  ├─> Calls OpenAI for summary
  ├─> Creates TaskSummaryRecord
  └─> Updates task with summary

SynthesizeProjectDocumentationAsync()
  ├─> Validates tasks list
  ├─> Creates task records
  ├─> Builds module index
  ├─> Builds dependency graph
  ├─> Generates architecture summary
  └─> Returns ProjectDocumentation

ExportMarkdownAsync()
  ├─> Generates formatted markdown
  ├─> Writes to file with error handling
  └─> Logs success/failure

ExportJsonAsync()
  ├─> Serializes ProjectDocumentation
  ├─> Writes to file with error handling
  └─> Logs success/failure

ExportJsonlDatasetAsync()
  ├─> Streams records to file
  ├─> One JSON line per task
  └─> Includes instruction/input/output/metadata
```

**UserInteractionService**
```csharp
HandleTaskUncertaintyAsync()
  ├─> Detects vague terms
  ├─> Identifies missing specs
  ├─> Prompts user for clarification
  └─> Enriches task context

ReviewTasksWithUserAsync()
  ├─> Displays task breakdown
  ├─> Requests confirmation
  └─> Allows cancellation

AskForClarificationAsync()
  └─> Interactive user prompt
```

### 3. Core Layer

**ParallelExecutionEngine**
```csharp
ExecuteTasksAsync()
  ├─> Builds task dependency graph
  ├─> While incomplete tasks exist:
  │   ├─> Find ready tasks (dependencies met)
  │   ├─> Execute in parallel (Task.WhenAll)
  │   ├─> Generate summaries after validation (NEW)
  │   └─> Mark completed
  └─> Returns all tasks

ExecuteTaskWithValidationAsync()
  ├─> Handle task uncertainty
  ├─> Generate code via OpenAI
  ├─> Validate code
  ├─> Track ValidationAttemptCount (NEW)
  ├─> If invalid:
  │   ├─> Re-prompt with errors
  │   └─> Retry (up to MaxRetries)
  ├─> Generate task summary (NEW)
  └─> Return validated task

GenerateSummariesForAllTasksAsync() // NEW
  ├─> Iterate through all tasks
  ├─> Generate summary for each
  └─> Handle failures gracefully

TopologicalSort()
  └─> Orders tasks respecting dependencies
```

**AoTEngineOrchestrator**
```csharp
ExecuteAsync()
  ├─> Step 1: Decompose request
  │   ├─> Call OpenAI
  │   └─> Review with user
  ├─> Step 1.25: Contract generation (NEW - when enableContractFirst=true)
  │   ├─> Analyze tasks for types needing contracts
  │   ├─> Generate frozen enums, interfaces, models
  │   ├─> Create ContractCatalog
  │   ├─> Save contracts.json manifest
  │   └─> Set up contract-aware OpenAI context
  ├─> Step 1.5: Complexity analysis
  │   ├─> Analyze each task for complexity
  │   ├─> Decompose tasks >300 lines
  │   └─> Update task list with subtasks
  ├─> Step 2: Execute tasks
  │   ├─> Parallel execution with validation
  │   └─> Contract-aware code generation (if enabled)
  ├─> Step 3: Validate contracts
  ├─> Step 4: Merge code
  ├─> Step 5: Generate report
  ├─> Step 6: Synthesize documentation
  │   ├─> Generate project documentation
  │   ├─> Export markdown
  │   ├─> Export JSON
  │   └─> Export JSONL
  └─> Return AoTResult with documentation and ContractCatalog
```

## Data Flow

### Request Processing Flow

```
User Request
    │
    ▼
Decomposition (OpenAI)
    │
    ▼
User Review & Clarification
    │
    ▼
Contract Generation (NEW - when enableContractFirst=true)
    ├─> Analyze tasks for contract needs
    ├─> Generate frozen enums
    ├─> Generate frozen interfaces
    ├─> Generate frozen models
    ├─> Generate frozen abstract classes
    ├─> Save contracts.json manifest
    └─> Set up contract-aware context
    │
    ▼
Complexity Analysis
    ├─> Analyze each task
    ├─> Split tasks >300 lines
    └─> Update dependencies
    │
    ▼
Task Graph Construction
    │
    ▼
Parallel Execution Loop:
    ├─> Identify Ready Tasks
    ├─> Handle Uncertainties
    ├─> Generate Code (OpenAI - contract-aware)
    ├─> Per-Atom Compilation Check (NEW)
    ├─> Validate Against Contracts (NEW)
    ├─> Auto-Fix Loop (NEW)
    ├─> Validate Code (Roslyn)
    ├─> Track Validation Attempts
    ├─> Generate Task Summary
    ├─> Retry if Failed
    └─> Mark Completed
    │
    ▼
Contract Validation
    │
    ▼
Code Merging
    │
    ▼
Final Validation
    │
    ▼
Report Generation
    │
    ▼
Documentation Synthesis
    ├─> Project Documentation
    ├─> Architecture Summary
    └─> Training Dataset
    │
    ▼
Output (Final Code + Report + Documentation + Contracts)
```

### Contract Generation Flow (NEW)

```
Task Decomposition Complete
    │
    ├─> When enableContractFirst=true
    │
    ▼
Analyze Tasks for Contract Needs
    ├─> Find types mentioned in multiple tasks
    ├─> Identify interfaces needed
    ├─> Identify shared enums
    ├─> Identify DTOs/models
    └─> Identify abstract base classes
    │
    ▼
Generate Contract Catalog
    ├─> EnumContract[] - with member lists
    ├─> InterfaceContract[] - with method signatures
    ├─> ModelContract[] - with properties
    └─> AbstractClassContract[] - with abstract methods
    │
    ▼
Freeze Contracts
    ├─> Set IsFrozen = true
    └─> Set FrozenAtUtc timestamp
    │
    ▼
Persist Contracts
    ├─> Save contracts.json manifest
    └─> Generate .cs contract files (optional)
    │
    ▼
Set Up Contract-Aware Context
    ├─> Configure PromptContextBuilder
    ├─> Register in SymbolTable
    └─> Update TypeRegistry
    │
    ▼
Continue to Complexity Analysis
```

### Documentation Generation Flow (NEW)

```
Validated Task
    │
    ├─> Immediately after validation passes
    │
    ▼
Generate Task Summary
    ├─> Call OpenAI with task code
    ├─> Parse structured JSON response
    │   {
    │     "purpose": "...",
    │     "key_behaviors": [...],
    │     "edge_cases": [...]
    │   }
    ├─> Create TaskSummaryRecord
    └─> Update TaskNode.Summary
    │
    ▼
(After all tasks complete)
    │
    ▼
Synthesize Project Documentation
    ├─> Collect all TaskSummaryRecords
    ├─> Build ModuleIndex (type → task)
    ├─> Build DependencyGraphSummary
    ├─> Generate ArchitectureSummary
    └─> Create ProjectDocumentation
    │
    ▼
Export Documentation
    ├─> Markdown (Documentation.md)
    ├─> JSON (Documentation.json)
    └─> JSONL (training_data.jsonl)
```

### Parallel Execution Example

```
Initial State:
task1 (ready) ──┐
task2 (ready) ──┼─> Execute in parallel via Task.WhenAll
task3 (ready) ──┘
                │
                ├─> Validate each
                └─> Generate summaries (NEW)

After Round 1:
task4 (depends on task1,task2) ──┐
task5 (depends on task2)         ├─> Execute in parallel
task6 (depends on task3)         ┘

After Round 2:
task7 (depends on task4,task5,task6) ─> Execute

Complete!
    │
    ▼
Synthesize documentation from all summaries
```

## Error Handling Strategy

### Validation Failures
1. Capture compilation errors from Roslyn
2. Track ValidationAttemptCount (NEW)
3. Re-prompt OpenAI with error details
4. Retry up to MaxRetries (default: 3)
5. Include attempt count in documentation (NEW)
6. Fail if still invalid after retries

### OpenAI API Errors
1. Catch HttpRequestException separately
2. Implement exponential backoff
3. Retry with delay
4. Report specific error to user

### Dependency Resolution Failures
1. Detect circular dependencies
2. Identify missing tasks
3. Report deadlock with task details
4. Throw InvalidOperationException

### Documentation Failures
1. Catch exceptions during summary generation
2. Fall back to minimal summary
3. Log warning but don't fail task
4. Continue with code generation
5. Handle file I/O errors gracefully

### Contract Violations (NEW)
1. Detect missing interface implementations
2. Detect signature mismatches
3. Detect invalid enum member usage
4. Detect sealed type inheritance attempts
5. Auto-fix loop attempts resolution
6. Report unresolvable violations to user

## Configuration

### appsettings.json
```json
{
  "OpenAI": {
    "ApiKey": "API key or use env var",
    "Model": "gpt-5.1 | gpt-4 | gpt-3.5-turbo"
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
```

### Environment Variables
- `OPENAI_API_KEY`: OpenAI API key (overrides appsettings)
- `OPENAI_MODEL`: Model selection (optional)

### Model Configuration

**Default Model (gpt-5.1)**: Used for general tasks
- Task decomposition
- Documentation generation
- Package version queries
- Summary generation
- Uses OpenAI SDK's ChatClient

**Code Generation Model (gpt-5.1-codex)**: Used for code generation
- Code generation (`GenerateCodeAsync`)
- Code regeneration (`RegenerateCodeWithErrorsAsync`)
- Contract-aware code generation (`GenerateCodeWithContractsAsync`)
- Uses direct HTTP calls via static shared HttpClient
- Provides fine-grained control over request/response
- Better performance for high-volume code generation

**Architecture Rationale**: The dual-model approach separates general language tasks from specialized code generation, allowing optimal model selection for each use case. Using HttpClient for code generation provides direct control over the API request/response, essential for the gpt-5.1-codex model's specific requirements.

## Extensibility Points

### Adding New Validators
Extend `CodeValidatorService` to add:
- Security scanning
- Performance analysis
- Custom linting rules

### Custom OpenAI Prompts
Override methods in `OpenAIService`:
- Custom decomposition strategies
- Domain-specific code generation
- Multi-language support
- Custom summary formats (NEW)

### Additional User Interactions
Extend `UserInteractionService`:
- Custom uncertainty detection
- Integration with issue tracking
- Collaborative review workflows

### Documentation Customization (NEW)
Extend `DocumentationService`:
- Custom export formats
- Additional metadata fields
- Integration with documentation systems
- Custom training data schemas

### Complexity Analysis Customization (NEW)
Extend `TaskComplexityAnalyzer`:
- Custom complexity scoring algorithms
- Domain-specific line count estimation
- Custom decomposition thresholds
- Additional complexity factors

## Performance Considerations

### Parallel Execution Benefits
- Independent tasks execute concurrently
- Reduces total execution time
- Scales with task graph width

### Documentation Generation (NEW)
- Summaries generated after validation (not blocking)
- Failures don't affect code generation
- Streaming export for large datasets

### Optimization Strategies
1. **Caching**: Cache OpenAI responses for identical prompts
2. **Batching**: Group similar tasks for efficient processing
3. **Lazy Loading**: Load references only when needed
4. **Connection Pooling**: Reuse HTTP connections to OpenAI
5. **Streaming**: Stream JSONL export to reduce memory usage (NEW)

## Security Considerations

1. **API Key Management**: Store in environment variables or secure vaults
2. **Code Injection**: Validate all generated code before execution
3. **Dependency Validation**: Ensure all assembly references are trusted
4. **User Input**: Sanitize user clarifications before including in prompts
5. **Output Sanitization**: Review generated code for security vulnerabilities
6. **Path Validation**: Validate output paths to prevent directory traversal (NEW)
7. **Training Data**: Review JSONL exports for sensitive content (NEW)

## Testing Strategy

### Unit Tests
- Model validation (TaskNode, ValidationResult, TaskSummaryRecord)
- Service isolation (CodeValidator, CodeMerger, DocumentationService)
- Engine logic (TopologicalSort, dependency resolution)
- Documentation export formats (NEW)

### Integration Tests
- OpenAI API integration
- End-to-end workflow
- Error handling scenarios
- Documentation generation flow (NEW)

### Manual Testing
- User interaction flows
- Complex task decompositions
- Validation retry logic
- Documentation output quality (NEW)
