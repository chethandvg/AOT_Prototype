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

5. **Integration:**
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
    "MaxRetries": 5
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
var executionEngine = new ParallelExecutionEngine(
    openAIService, 
    validatorService,
    userInteractionService);
var mergerService = new CodeMergerService(validatorService);
var orchestrator = new AoTEngineOrchestrator(
    openAIService, 
    executionEngine, 
    mergerService,
    userInteractionService);

var result = await orchestrator.ExecuteAsync(
    "Create a microservice for user management",
    "Use ASP.NET Core 8.0 and Entity Framework"
);

if (result.Success)
{
    Console.WriteLine(result.FinalCode);
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

## Tips for Best Results

1. **Be Specific**: Provide clear requirements to minimize uncertainties
2. **Include Context**: Specify frameworks, patterns, and technologies to use
3. **Review Decomposition**: Always review the task breakdown before execution
4. **Provide Clarifications**: Answer uncertainty prompts with detailed information
5. **Check Validation**: Review validation errors and provide additional context if needed

## Troubleshooting

### Issue: OpenAI API Key Error
**Solution:** Set the `OPENAI_API_KEY` environment variable or update `appsettings.json`

### Issue: Validation Failures
**Solution:** The engine will automatically retry with error feedback. Review the error messages in the console.

### Issue: Task Decomposition Unclear
**Solution:** Cancel and rephrase your request with more specific requirements

### Issue: Code Compilation Errors
**Solution:** The validator uses Roslyn; ensure generated code follows C# syntax. The engine will retry up to 3 times with error feedback.
