# AoT Engine Usage Examples

This guide covers usage for both **AoTEngine** (original) and **AoTEngine.AtomicAgent** (new).

## 🚀 Quick Start

### Option 1: AoTEngine (Original - Task-Based Parallel)

```bash
cd src/AoTEngine
export OPENAI_API_KEY="your-api-key"
dotnet run
```

**Best for**: Parallel execution, contract-first generation, documentation synthesis

### Option 2: AoTEngine.AtomicAgent (NEW - DAG-Based Autonomous)

```bash
cd src/AoTEngine.AtomicAgent
export OPENAI_API_KEY="your-api-key"
dotnet run
```

**Best for**: Clean architecture enforcement, dependency management, persistent state tracking

**Quick Start Guide**: See [src/AoTEngine.AtomicAgent/GETTING_STARTED.md](src/AoTEngine.AtomicAgent/GETTING_STARTED.md)

---

## AoTEngine Usage Examples

### Example 1: Simple Calculator

```bash
cd src/AoTEngine
export OPENAI_API_KEY="your-api-key"
dotnet run
```

**Input:**
```
Create a simple calculator class in C# that can add, subtract, multiply, and divide two numbers.
```

**Expected Output:**
1. Task decomposition into:
   - Task 1: Create calculator class with basic operations
   - Task 2: Add input validation
   - Task 3: Create unit tests

2. User confirmation prompt
3. Parallel execution of independent tasks
4. Code validation and merging
5. Final output saved to `generated_code.cs`
6. Documentation files (if output directory specified):
   - `Documentation.md`
   - `Documentation.json`
   - `training_data.jsonl`

### Example 2: REST API with Authentication

**Input:**
```
Create a REST API for managing a todo list with JWT authentication
```

**Expected Workflow:**

1. **Decomposition Phase:**
   ```
   Task task1: Create authentication service
   Task task2: Create todo item model
   Task task3: Create todo repository
   Task task4: Create todo API controller
   Task task5: Add JWT middleware
   ```

2. **Uncertainty Detection:**
   ```
   ⚠️  UNCERTAINTY DETECTED
   Question: What type of API should be created? (REST/GraphQL/gRPC)
   Your response: REST API using ASP.NET Core
   
   Question: Which database technology should be used?
   Your response: Entity Framework with SQL Server
   ```

3. **Parallel Execution:**
   - Tasks 1, 2, 3 run in parallel (no dependencies)
   - Tasks 4, 5 run after dependencies complete

4. **Validation:**
   - Each task's code is compiled using Roslyn
   - Linting checks performed
   - Retry with error feedback if validation fails

5. **Documentation Generation:** (NEW)
   - Per-task summaries generated after validation
   - Project documentation synthesized
   - Training dataset exported

6. **Integration:**
   - Contracts validated
   - Code merged into final solution
   - Execution report generated

## Advanced Usage

### Custom Configuration

Edit `appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-5.1"
  },
  "Engine": {
    "MaxRetries": 5,
    "UseBatchValidation": true,
    "UseHybridValidation": true,
    "MaxLinesPerTask": 300,
    "EnableComplexityAnalysis": true
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

```bash
export OPENAI_API_KEY="sk-..."
export OPENAI_MODEL="gpt-5.1"
```

### Programmatic Usage

```csharp
using AoTEngine.Core;
using AoTEngine.Services;

// Default model is gpt-5.1 for general tasks
// Code generation automatically uses gpt-5.1-codex via HttpClient
var openAIService = new OpenAIService(apiKey, "gpt-5.1");
var validatorService = new CodeValidatorService();
var userInteractionService = new UserInteractionService();

// Create documentation service with configuration (NEW)
var docConfig = new DocumentationConfig
{
    Enabled = true,
    GeneratePerTask = true,
    GenerateProjectSummary = true,
    ExportMarkdown = true,
    ExportJson = true,
    ExportJsonl = true,
    SummaryModel = "gpt-4o-mini"
};
var documentationService = new DocumentationService(openAIService, docConfig);

var executionEngine = new ParallelExecutionEngine(
    openAIService, 
    validatorService,
    userInteractionService,
    buildService: null,
    outputDirectory: "./output",
    documentationService: documentationService);

var mergerService = new CodeMergerService(validatorService);

var orchestrator = new AoTEngineOrchestrator(
    openAIService, 
    executionEngine, 
    mergerService,
    userInteractionService,
    validatorService,
    documentationService,
    docConfig);

var result = await orchestrator.ExecuteAsync(
    "Create a microservice for user management",
    "Use ASP.NET Core 8.0 and Entity Framework",
    useBatchValidation: true,
    useHybridValidation: false,
    outputDirectory: "./output",
    maxLinesPerTask: 300,           // Max lines per generated task
    enableComplexityAnalysis: true,  // Enable automatic decomposition
    enableContractFirst: true,       // Enable Contract-First generation (NEW)
    projectName: "UserService"       // Project name for namespace prefixing (NEW)
);

if (result.Success)
{
    Console.WriteLine(result.FinalCode);
    
    // Access documentation
    Console.WriteLine(result.FinalDocumentation);
    Console.WriteLine($"Markdown: {result.DocumentationPaths?.MarkdownPath}");
    Console.WriteLine($"JSON: {result.DocumentationPaths?.JsonPath}");
    Console.WriteLine($"JSONL: {result.DocumentationPaths?.JsonlDatasetPath}");
    
    // Access frozen contracts (NEW)
    if (result.ContractCatalog != null)
    {
        Console.WriteLine($"Frozen Interfaces: {result.ContractCatalog.Interfaces.Count}");
        Console.WriteLine($"Frozen Enums: {result.ContractCatalog.Enums.Count}");
        Console.WriteLine($"Frozen Models: {result.ContractCatalog.Models.Count}");
    }
}
```

## Example Scenarios

### 1. Microservice Development
```
Request: Create a microservice for product catalog with CRUD operations, caching, and API documentation
```

### 2. Data Processing Pipeline
```
Request: Build a data processing pipeline that reads CSV files, validates data, transforms it, and saves to database
```

### 3. Testing Infrastructure
```
Request: Create unit tests and integration tests for a shopping cart service with mock data
```

### 4. Utility Library
```
Request: Create a utility library for string manipulation including validation, formatting, and parsing functions
```

## User Interaction Examples

### Uncertainty: Vague Requirements

**Input:** "Create a fast and efficient sorting algorithm"

**System Prompt:**
```
⚠️  UNCERTAINTY DETECTED
Question: What are the efficiency requirements? (time/space/both)
Your response: Time complexity should be O(n log n) or better
```

### Uncertainty: Missing Specifications

**Input:** "Create a database service"

**System Prompts:**
```
⚠️  UNCERTAINTY DETECTED
Question: Which database technology should be used? (SQL Server/PostgreSQL/MongoDB/etc.)
Your response: PostgreSQL with Dapper

Question: What operations should the service support?
Your response: CRUD operations with transaction support
```

### Task Review

```
📋 Task Decomposition Review
═══════════════════════════════════════════════════════════
The request has been decomposed into 4 tasks:

Task task1: Create database connection manager
  Dependencies: None

Task task2: Create repository base class
  Dependencies: task1

Task task3: Implement CRUD operations
  Dependencies: task2

Task task4: Add transaction support
  Dependencies: task3

Does this task decomposition look correct? (y/n): y
```

## Output Structure

### Execution Report
```
=== AoT Engine Execution Report ===

Total Tasks: 4
Completed Tasks: 4
Validated Tasks: 4

Task Details:
  - task1: Create database connection manager
    Dependencies: None
    Status: ✓ Completed, ✓ Validated
  - task2: Create repository base class
    Dependencies: task1
    Status: ✓ Completed, ✓ Validated
  ...

Merged Code Length: 2847 characters
Merged Code Lines: 118

✅ Documentation synthesis complete.
   📄 Exported Markdown documentation to: ./output/Documentation.md
   📄 Exported JSON documentation to: ./output/Documentation.json
   📄 Exported JSONL training dataset to: ./output/training_data.jsonl
```

### Generated Code Structure
```csharp
using System;
using System.Data;
using Npgsql;

namespace DatabaseService
{
    public class ConnectionManager
    {
        // Generated code from task1
    }

    public abstract class RepositoryBase<T>
    {
        // Generated code from task2
    }

    // Additional classes from other tasks
}
```

### Documentation Output (NEW)

#### Documentation.md
```markdown
# Project Documentation

**Generated:** 2024-01-15 10:30:00 UTC

## Original Request
Create a database service

## Architecture Overview
The project implements a layered database service architecture...

## Task Summaries

### task1: Create database connection manager
**Namespace:** `DatabaseService`
**Purpose:** Manages PostgreSQL database connections with connection pooling
**Key Behaviors:**
- Connection string configuration
- Connection pooling
- Automatic reconnection
**Validation:** Passed on first attempt

---

### task2: Create repository base class
**Dependencies:** task1
**Purpose:** Abstract base class for data repositories
...
```

#### training_data.jsonl
```json
{"instruction":"Generate C# code for: Create database connection manager","input":{"task_description":"Create database connection manager","dependencies":[],"expected_types":["ConnectionManager"],"namespace":"DatabaseService"},"output":"// C# code...","metadata":{"task_id":"task1","purpose":"Manages database connections","validation_notes":"Passed on first attempt"}}
{"instruction":"Generate C# code for: Create repository base class","input":{"task_description":"Create repository base class","dependencies":["task1"],"expected_types":["RepositoryBase"]},"output":"// C# code...","metadata":{"task_id":"task2","purpose":"Abstract base for repositories"}}
```

## Tips for Best Results

1. **Be Specific**: Provide clear requirements to minimize uncertainties
2. **Include Context**: Specify frameworks, patterns, and technologies to use
3. **Review Decomposition**: Always review the task breakdown before execution
4. **Provide Clarifications**: Answer uncertainty prompts with detailed information
5. **Check Validation**: Review validation errors and provide additional context if needed
6. **Review Documentation**: Check generated documentation for accuracy (NEW)
7. **Use Training Data**: Leverage JSONL exports for fine-tuning models (NEW)
8. **Large Tasks**: The engine automatically splits tasks >300 lines into smaller subtasks (NEW)

## Complexity Analysis Configuration (NEW)

### Maximum Lines Per Task
```json
{
  "Engine": {
    "MaxLinesPerTask": 300,
    "EnableComplexityAnalysis": true
  }
}
```

### Disabling Complexity Analysis
```json
{
  "Engine": {
    "EnableComplexityAnalysis": false
  }
}
```

### Custom Line Threshold
```json
{
  "Engine": {
    "MaxLinesPerTask": 500
  }
}
```

## Documentation Configuration (NEW)

### Disabling Documentation
```json
{
  "Documentation": {
    "Enabled": false
  }
}
```

### Selective Export
```json
{
  "Documentation": {
    "Enabled": true,
    "ExportMarkdown": true,
    "ExportJson": false,
    "ExportJsonl": true
  }
}
```

### Custom Summary Model
```json
{
  "Documentation": {
    "SummaryModel": "gpt-4",
    "MaxSummaryTokens": 500
  }
}
```

## Contract-First Configuration (NEW)

### Enabling Contract-First Generation
```json
{
  "Engine": {
    "EnableContractFirst": true
  }
}
```

### Programmatic Usage
```csharp
var result = await orchestrator.ExecuteAsync(
    userRequest,
    context: "",
    useBatchValidation: true,
    useHybridValidation: false,
    outputDirectory: "./output",
    enableContractFirst: true,    // Enable Contract-First
    projectName: "MyProject"      // Project name for namespaces
);

// Access frozen contracts
if (result.ContractCatalog != null)
{
    // Inspect frozen interfaces
    foreach (var iface in result.ContractCatalog.Interfaces)
    {
        Console.WriteLine($"Interface: {iface.FullyQualifiedName}");
        foreach (var method in iface.Methods)
        {
            Console.WriteLine($"  - {method.ReturnType} {method.Name}(...)");
        }
    }
    
    // Inspect frozen enums
    foreach (var enumContract in result.ContractCatalog.Enums)
    {
        Console.WriteLine($"Enum: {enumContract.Name}");
        Console.WriteLine($"  Members: {string.Join(", ", enumContract.Members.Select(m => m.Name))}");
    }
}
```

### Contract Manifest File
When Contract-First is enabled, a `contracts.json` manifest is saved:
```json
{
  "projectName": "MyProject",
  "rootNamespace": "MyProject",
  "isFrozen": true,
  "frozenAtUtc": "2025-12-25T10:30:00Z",
  "enums": [...],
  "interfaces": [...],
  "models": [...],
  "abstractClasses": [...]
}
```

### Output Files with Contract-First
```
outputDirectory/
├── contracts.json              # Contract manifest (NEW)
├── MyProject.Contracts/        # Contract .cs files (NEW)
│   ├── IMyInterface.cs
│   ├── MyEnum.cs
│   └── MyModel.cs
├── generated_code.cs
├── Documentation.md
├── Documentation.json
└── training_data.jsonl
```

## Troubleshooting

### Issue: OpenAI API Key Error
**Solution:** Set the `OPENAI_API_KEY` environment variable or update `appsettings.json`

### Issue: Validation Failures
**Solution:** The engine will automatically retry with error feedback. Review the error messages in the console.

### Issue: Task Decomposition Unclear
**Solution:** Cancel and rephrase your request with more specific requirements

### Issue: Code Compilation Errors
**Solution:** The validator uses Roslyn; ensure generated code follows C# syntax. The engine will retry up to 3 times with error feedback.

### Issue: Documentation Generation Fails
**Solution:** Documentation failures don't affect code generation. Check console for warnings. Ensure OpenAI API is accessible for summary generation.

### Issue: Empty Training Dataset
**Solution:** Ensure `ExportJsonl` is enabled and tasks have generated code. Check output directory permissions.

### Issue: Task Automatically Split
**Explanation:** Tasks estimated to generate more than 300 lines are automatically decomposed into smaller subtasks. This is expected behavior and helps maintain code manageability.

### Issue: Unexpected Subtask Count
**Solution:** Adjust `MaxLinesPerTask` in configuration. Lower values create more subtasks, higher values allow larger code blocks. Default is 300 lines.

### Issue: Duplicate Type Definitions (NEW)
**Solution:** The enhanced code integration pipeline automatically detects and resolves duplicate type definitions. Use `MergeWithIntegrationAsync()` for advanced conflict resolution. Check the `Conflicts` property in `MergeResult` for detected conflicts.

### Issue: Missing Using Statements After Merge (NEW)
**Solution:** The IntegrationFixer automatically adds missing using statements. Ensure `EnableAutoFix = true` in `MergeOptions`.

### Issue: Ambiguous Type References (NEW)
**Solution:** When types with the same name exist in multiple namespaces, the IntegrationFixer can fully qualify type names. You can also add custom type-to-namespace mappings using `IntegrationFixer.AddTypeNamespaceMapping()`.

### Issue: Interface Implementation Missing (Contract-First)
**Solution:** When Contract-First is enabled, the AutoFixService attempts to add missing interface implementations. If auto-fix fails, the error message will include the exact method signatures required. Ensure your implementation matches the frozen contract exactly.

### Issue: Sealed Type Inheritance Error (Contract-First)
**Solution:** The engine detects attempts to inherit from sealed types and suggests using composition instead. The AutoFixService can automatically convert `class A : SealedBase` to composition with a private field.

### Issue: Invalid Enum Member Usage (Contract-First)
**Solution:** When using frozen enums, only members defined in the contract are valid. Check `result.ContractCatalog.Enums` for valid members. The AtomCompilationService detects invalid enum member usage early.

### Issue: Contract Violations Not Detected Early
**Solution:** Enable Contract-First with `enableContractFirst: true`. This runs per-atom compilation checks that catch contract violations before batch validation, reducing wasted generation attempts.

## Advanced Code Integration

The code integration system has been upgraded from simple concatenation to a sophisticated merge-with-rules pipeline.

### Using Advanced Integration

```csharp
var mergerService = new CodeMergerService(validatorService);

// Use the advanced integration pipeline
var mergeResult = await mergerService.MergeWithIntegrationAsync(tasks, new MergeOptions
{
    AutoResolveKeepFirst = true,      // Automatically keep first definition
    EnablePartialClassMerge = true,   // Convert compatible duplicates to partial classes
    EnableAutoFix = true,             // Enable Roslyn-based auto-fixes
    FailOnUnresolvableConflicts = false,  // Continue even with conflicts
    EnableManualCheckpoints = true    // Enable interactive conflict resolution
});

if (mergeResult.Success)
{
    Console.WriteLine("Merge successful!");
    Console.WriteLine(mergeResult.MergedCode);
}
else
{
    Console.WriteLine("Merge completed with issues:");
    foreach (var error in mergeResult.RemainingErrors)
    {
        Console.WriteLine($"  - {error}");
    }
}

// Review what was fixed
foreach (var fix in mergeResult.AppliedFixes)
{
    Console.WriteLine($"Applied fix: {fix}");
}

// Review detected conflicts
foreach (var conflict in mergeResult.Conflicts)
{
    Console.WriteLine($"Conflict: {conflict.FullyQualifiedName}");
    Console.WriteLine($"  Resolution: {conflict.SuggestedResolution}");
}
```

### Conflict Resolution Strategies

| Strategy | Description | When Used |
|----------|-------------|-----------|
| `KeepFirst` | Keep the first definition, discard duplicates | Interfaces, enums, records |
| `MergeAsPartial` | Convert both definitions to partial classes | Classes with non-conflicting members |
| `RemoveDuplicate` | Remove the duplicate member | Conflicting constructors/methods |
| `FailFast` | Fail the merge operation | Unresolvable conflicts |

### Interactive Conflict Resolution

For complex conflicts, enable manual checkpoints:

```csharp
var checkpointHandler = new IntegrationCheckpointHandler(interactive: true);

var mergeResult = await mergerService.MergeWithIntegrationAsync(
    tasks, 
    new MergeOptions { EnableManualCheckpoints = true },
    checkpointHandler);
```

The handler will display conflicts and prompt for resolution:

```
═══════════════════════════════════════════════════════════════════════
                    INTEGRATION CONFLICTS DETECTED                      
═══════════════════════════════════════════════════════════════════════

Conflict 1 of 2:
  Type: MyApp.IValidator
  Description: Type 'MyApp.IValidator' is defined in both task 'task1' and task 'task2'
  
  Recommended: Keep the first definition and discard the duplicate

  Available options:
    [K] Keep first definition, discard duplicate
    [R] Remove the duplicate
    [A] Abort the merge operation

Enter option [K/R/A] (default: K): 
```

## Checkpoint System

The checkpoint system automatically saves execution state during task execution, enabling progress tracking and recovery.

### Automatic Checkpoint Creation

When an output directory is specified, checkpoints are automatically saved after each task completes:

```
💾 Checkpoint saved: checkpoint_20251225_103000.json
```

### Checkpoint File Structure

```
outputDirectory/
├── checkpoints/
│   ├── checkpoint_20251225_103000.json  # Timestamped checkpoint
│   ├── checkpoint_20251225_103000.md    # Human-readable version
│   ├── checkpoint_20251225_104500.json  # Later checkpoint
│   ├── checkpoint_20251225_104500.md
│   ├── latest.json                       # Latest checkpoint (copy)
│   └── latest.md                         # Latest checkpoint (copy)
└── GeneratedCode_*/                      # Generated project files
```

### Checkpoint Contents

Each checkpoint includes:
- **Project Information**: Original request and description
- **Progress Tracking**: Total, completed, pending, and failed task counts
- **Completed Tasks**: Full details with generated code, validation status
- **Dependency Graph**: Task relationships and dependencies
- **Architecture Summary**: Current state of the architecture
- **Timestamps**: When checkpoint was created and tasks completed

### Example Checkpoint (JSON)

```json
{
  "checkpoint_timestamp": "2025-12-25T10:30:00Z",
  "project_request": "Create a simple calculator",
  "project_description": "Calculator with basic operations",
  "total_tasks": 5,
  "completed_tasks": 3,
  "pending_tasks": 2,
  "failed_tasks": 0,
  "completed_task_details": [
    {
      "task_id": "task1",
      "description": "Create calculator class",
      "dependencies": [],
      "expected_types": ["Calculator"],
      "namespace": "CalculatorApp",
      "generated_code": "public class Calculator { ... }",
      "validation_status": "validated",
      "validation_attempts": 1,
      "completed_at": "2025-12-25T10:25:00Z",
      "summary": "Calculator class with basic operations"
    }
  ],
  "pending_task_ids": ["task4", "task5"],
  "dependency_graph": {
    "task1": [],
    "task2": ["task1"],
    "task3": ["task1"]
  },
  "execution_status": "in_progress"
}
```

### Example Checkpoint (Markdown)

```markdown
# Execution Checkpoint

**Timestamp:** 2025-12-25 10:30:00 UTC
**Status:** in_progress

## Project Overview

**Request:** Create a simple calculator
**Description:** Calculator with basic operations

## Execution Progress

**Total Tasks:** 5
**Completed:** 3
**Pending:** 2
**Failed:** 0
**Progress:** 60.0% (3/5 tasks)

## Completed Tasks

### task1
**Description:** Create calculator class
**Namespace:** CalculatorApp
**Validation Status:** validated
**Validation Attempts:** 1

**Generated Code:**
```csharp
public class Calculator { ... }
```

## Pending Tasks
- task4
- task5

## Dependency Graph
- task1 → [None]
- task2 → [task1]
- task3 → [task1]
```

### Programmatic Usage

```csharp
// Load latest checkpoint
var checkpointService = new CheckpointService("./output");
var latestPath = checkpointService.GetLatestCheckpoint("./output");

if (latestPath != null)
{
    var checkpoint = await checkpointService.LoadCheckpointAsync(latestPath);
    
    if (checkpoint != null)
    {
        Console.WriteLine($"Completed: {checkpoint.CompletedTasks}/{checkpoint.TotalTasks}");
        Console.WriteLine($"Status: {checkpoint.ExecutionStatus}");

        // Examine completed tasks
        foreach (var task in checkpoint.CompletedTaskDetails)
        {
            Console.WriteLine($"Task {task.TaskId}: {task.Description}");
            var codePreview = task.GeneratedCode.Length > 100 
                ? task.GeneratedCode.Substring(0, 100) + "..." 
                : task.GeneratedCode;
            Console.WriteLine($"Code: {codePreview}");
        }
    }
    else
    {
        Console.WriteLine("Failed to load checkpoint.");
    }
}
else
{
    Console.WriteLine("No checkpoints found in the specified output directory.");
}
```

### Configuration Options

**Enable/Disable Checkpoints:**
```csharp
var executionEngine = new ParallelExecutionEngine(
    openAIService, 
    validatorService,
    userInteractionService,
    buildService,
    outputDirectory: "./output",
    documentationService: documentationService,
    saveCheckpoints: true);  // Default: true when outputDirectory exists
```

**Adjust Checkpoint Frequency:**
```csharp
var executionEngine = new ParallelExecutionEngine(
    openAIService, 
    validatorService,
    userInteractionService,
    buildService,
    outputDirectory: "./output",
    documentationService: documentationService,
    saveCheckpoints: true,
    checkpointFrequency: 5);  // Save every 5 tasks (default: 1)
```

### Use Cases

1. **Progress Monitoring**: Check `latest.md` to see current execution state
2. **Recovery Planning**: Understand what was completed before an interruption
3. **Debugging**: Examine generated code and dependencies at any checkpoint
4. **Audit Trail**: Track the evolution of the project over time
5. **Documentation**: Human-readable progress reports for stakeholders

### Checkpoint vs Documentation

- **Checkpoints**: Incremental snapshots saved during execution
  - Created after each task (or every N tasks)
  - Shows work in progress
  - Includes pending and failed tasks
  - Useful for recovery and monitoring
  
- **Documentation**: Final comprehensive documentation
  - Created after all tasks complete
  - Shows final architecture
  - Includes only successful tasks
  - Useful for understanding the complete project

---

## AoTEngine.AtomicAgent Usage Examples

### Overview

The AtomicAgent uses a different paradigm from the original AoTEngine:
- **DAG-based** instead of task-based
- **Persistent state** via `solution_manifest.json`
- **Abstractions first** with topological sort
- **Per-atom validation** with Roslyn feedback loop

### Example 1: User Management System

**Request:**
```
Create a user management system with User DTO, IUserRepository interface, 
and InMemoryUserRepository implementation
```

**Workflow:**

1. **Clarification Phase**:
   ```
   ⚠️  UNCERTAINTY DETECTED
   1. Storage type? → memory
   2. Interface type? → console
   3. Frameworks? → none
   ```

2. **Planning Phase** (Abstractions First):
   ```
   Generated 3 atoms:
   - atom_001 (dto): UserDto [Core layer, no deps]
   - atom_002 (interface): IUserRepository [Core layer, deps: atom_001]
   - atom_003 (implementation): InMemoryUserRepository [Infrastructure layer, deps: atom_001, atom_002]
   ```

3. **Execution Phase**:
   ```
   → Executing atom_001: UserDto
      ✓ Compiled successfully
      ✓ Semantic extraction: Added UserDto to symbol table
   
   → Executing atom_002: IUserRepository
      ✓ Compiled successfully
      ✓ Semantic extraction: Added IUserRepository.GetById, Add, Update, Delete
   
   → Executing atom_003: InMemoryUserRepository
      ✓ Compiled successfully (retry 0)
   ```

4. **Output**:
   ```
   ./output/
   ├── src/
   │   ├── Core/
   │   │   ├── dtos/UserDto.cs
   │   │   └── interfaces/IUserRepository.cs
   │   └── Infrastructure/
   │       └── implementations/InMemoryUserRepository.cs
   └── solution_manifest.json
   ```

### Example 2: Multi-Layer Application with Validation

**Request:**
```
Create an order processing system with Order and OrderItem DTOs,
IOrderService interface, OrderService implementation,
and OrderController for REST API
```

**Generated Atoms** (showing topological order):
```
1. atom_001 (dto): OrderItemDto [Core, no deps]
2. atom_002 (dto): OrderDto [Core, deps: atom_001]
3. atom_003 (interface): IOrderService [Core, deps: atom_001, atom_002]
4. atom_004 (implementation): OrderService [Infrastructure, deps: atom_001, atom_002, atom_003]
5. atom_005 (implementation): OrderController [Presentation, deps: atom_003]
```

**Architectural Validation**:
- ✓ Core atoms have zero dependencies on Infrastructure/Presentation
- ✓ Infrastructure depends only on Core
- ✓ Presentation depends on Core (via IOrderService)
- ✗ If OrderController tried to depend on OrderService directly → **REJECTED**

### Example 3: Handling Compilation Errors

**Scenario**: Generated code has a typo

**Self-Correction Cycle**:
```
→ Executing atom_003: UserRepository
   ✗ Compilation failed:
      CS1061 at Line 15: 'User' does not contain a definition for 'Naem'
   
   → Retry 1: Feeding error to LLM
      "Fix the following compilation error: ..."
   
   ✓ Recompiled successfully
      Fixed: Naem → Name
```

### Example 4: Circular Dependency Detection

**Request:**
```
Create ServiceA that depends on ServiceB,
and ServiceB that depends on ServiceA
```

**Planner Response**:
```
⚠️  Circular dependency detected in atoms: 
   atom_002 → atom_003 → atom_002

→ Retry 1: Refactoring to break cycle
   Introducing IServiceA interface
   
Generated 4 atoms:
- atom_001 (interface): IServiceA
- atom_002 (interface): IServiceB
- atom_003 (implementation): ServiceA [deps: atom_001, atom_002]
- atom_004 (implementation): ServiceB [deps: atom_001, atom_002]
```

### Configuration Examples

#### Custom Output Directory
```bash
dotnet run
# When prompted:
📁 Enter output directory: /path/to/my/project
```

#### Disable Abstractions First (not recommended)
```json
// appsettings.json
"Planner": {
  "AbstractionsFirst": false
}
```

#### Increase Retry Count
```json
"Execution": {
  "MaxRetries": 5
}
```

#### Disable Hot Cache (reduce memory usage)
```json
"Context": {
  "EnableHotCache": false
}
```

### Reading the Solution Manifest

The `solution_manifest.json` provides a complete picture:

```json
{
  "project_metadata": {
    "name": "MyProject",
    "created_at": "2024-01-01T10:00:00Z"
  },
  "semantic_symbol_table": {
    "interfaces": [
      {
        "name": "IUserRepository",
        "namespace": "MyProject.Core",
        "methods": [
          "User GetById(int id)",
          "void Add(User user)"
        ]
      }
    ]
  },
  "atoms": [
    {
      "id": "atom_001",
      "status": "completed",
      "retry_count": 0,
      "compile_errors": []
    }
  ]
}
```

### Comparison: AoTEngine vs AtomicAgent

| Scenario | AoTEngine | AtomicAgent |
|----------|-----------|-------------|
| Simple CRUD | ✓ Fast parallel execution | ✓ Clean architecture enforced |
| Complex dependencies | ⚠️ Manual ordering needed | ✓ Automatic topological sort |
| Architectural rules | ⚠️ No enforcement | ✓ Validates Core has zero deps |
| State tracking | In-memory only | ✓ Persistent JSON manifest |
| Token efficiency | Full code in context | ✓ 70% reduction via signatures |
| Self-correction | Batch validation | ✓ Per-atom with Roslyn feedback |

### When to Use Which?

**Use AoTEngine when:**
- You need parallel execution for speed
- You want contract-first generation
- You need documentation synthesis (Markdown, JSON, JSONL)
- You have a well-defined structure

**Use AtomicAgent when:**
- You need strict Clean Architecture enforcement
- You have complex dependencies to manage
- You want persistent state tracking
- You need self-correcting compilation feedback
- Token efficiency is critical

### Additional Resources

- **Getting Started**: [src/AoTEngine.AtomicAgent/GETTING_STARTED.md](src/AoTEngine.AtomicAgent/GETTING_STARTED.md)
- **Implementation Details**: [ATOMIC_AGENT_IMPLEMENTATION_SUMMARY.md](ATOMIC_AGENT_IMPLEMENTATION_SUMMARY.md)
- **Project README**: [src/AoTEngine.AtomicAgent/README.md](src/AoTEngine.AtomicAgent/README.md)
