# AoT Engine Usage Examples

## Basic Usage

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
    "Model": "gpt-4"
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
export OPENAI_MODEL="gpt-4-turbo"
```

### Programmatic Usage

```csharp
using AoTEngine.Core;
using AoTEngine.Services;

var openAIService = new OpenAIService(apiKey, "gpt-4");
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
    maxLinesPerTask: 300,           // Max lines per generated task (NEW)
    enableComplexityAnalysis: true   // Enable automatic decomposition (NEW)
);

if (result.Success)
{
    Console.WriteLine(result.FinalCode);
    
    // Access documentation (NEW)
    Console.WriteLine(result.FinalDocumentation);
    Console.WriteLine($"Markdown: {result.DocumentationPaths?.MarkdownPath}");
    Console.WriteLine($"JSON: {result.DocumentationPaths?.JsonPath}");
    Console.WriteLine($"JSONL: {result.DocumentationPaths?.JsonlDatasetPath}");
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

## Troubleshooting

### Issue: OpenAI API Key Error
**Solution:** Set the `OPENAI_API_KEY` environment variable or update `appsettings.json`

### Issue: Validation Failures
**Solution:** The engine will automatically retry with error feedback. Review the error messages in the console.

### Issue: Task Decomposition Unclear
**Solution:** Cancel and rephrase your request with more specific requirements

### Issue: Code Compilation Errors
**Solution:** The validator uses Roslyn; ensure generated code follows C# syntax. The engine will retry up to 3 times with error feedback.

### Issue: Documentation Generation Fails (NEW)
**Solution:** Documentation failures don't affect code generation. Check console for warnings. Ensure OpenAI API is accessible for summary generation.

### Issue: Empty Training Dataset (NEW)
**Solution:** Ensure `ExportJsonl` is enabled and tasks have generated code. Check output directory permissions.

### Issue: Task Automatically Split (NEW)
**Explanation:** Tasks estimated to generate more than 300 lines are automatically decomposed into smaller subtasks. This is expected behavior and helps maintain code manageability.

### Issue: Unexpected Subtask Count (NEW)
**Solution:** Adjust `MaxLinesPerTask` in configuration. Lower values create more subtasks, higher values allow larger code blocks. Default is 300 lines.
