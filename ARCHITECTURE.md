# AoT Engine Architecture

## System Overview

The AoT (Atom of Thought) Engine is designed around four core principles:
1. **Decomposition**: Break complex tasks into atomic units
2. **Parallelism**: Execute independent tasks concurrently
3. **Validation**: Ensure code quality through compilation and linting
4. **Interaction**: Engage users when uncertainties arise

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
│             │  │   Resolution     │  │ • Generate     │
│             │  │                  │  │   Report       │
└─────────────┘  └──────┬───────────┘  └────────────────┘
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
              └─────────────────────┘
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
```

## Component Details

### 1. Models Layer

**TaskNode**
- Represents an atomic task in the DAG
- Properties: Id, Description, Dependencies, GeneratedCode, ValidationStatus
- Tracks: Completion status, retry count, validation errors

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
DecomposeTaskAsync()
  ├─> Sends structured prompt to GPT
  ├─> Parses JSON response into TaskNodes
  └─> Returns TaskDecompositionResponse

GenerateCodeAsync()
  ├─> Builds context from dependencies
  ├─> Sends code generation prompt
  └─> Returns C# code snippet

RegenerateCodeWithErrorsAsync()
  ├─> Includes validation errors in prompt
  ├─> Requests corrected code
  └─> Returns improved snippet
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

**CodeMergerService**
```csharp
MergeCodeSnippetsAsync()
  ├─> Extracts using statements
  ├─> Organizes by namespace
  ├─> Merges into cohesive solution
  └─> Validates merged code

ValidateContracts()
  ├─> Checks dependency satisfaction
  ├─> Validates all tasks completed
  └─> Returns contract validation result

CreateExecutionReport()
  └─> Generates summary statistics
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
  │   └─> Mark completed
  └─> Returns all tasks

ExecuteTaskWithValidationAsync()
  ├─> Handle task uncertainty
  ├─> Generate code via OpenAI
  ├─> Validate code
  ├─> If invalid:
  │   ├─> Re-prompt with errors
  │   └─> Retry (up to MaxRetries)
  └─> Return validated task

TopologicalSort()
  └─> Orders tasks respecting dependencies
```

**AoTEngineOrchestrator**
```csharp
ExecuteAsync()
  ├─> Step 1: Decompose request
  │   ├─> Call OpenAI
  │   └─> Review with user
  ├─> Step 2: Execute tasks
  │   └─> Parallel execution with validation
  ├─> Step 3: Validate contracts
  ├─> Step 4: Merge code
  └─> Step 5: Generate report
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
Task Graph Construction
    │
    ▼
Parallel Execution Loop:
    ├─> Identify Ready Tasks
    ├─> Handle Uncertainties
    ├─> Generate Code (OpenAI)
    ├─> Validate Code (Roslyn)
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
Output (Final Code + Report)
```

### Parallel Execution Example

```
Initial State:
task1 (ready) ──┐
task2 (ready) ──┼─> Execute in parallel via Task.WhenAll
task3 (ready) ──┘

After Round 1:
task4 (depends on task1,task2) ──┐
task5 (depends on task2)         ├─> Execute in parallel
task6 (depends on task3)         ┘

After Round 2:
task7 (depends on task4,task5,task6) ─> Execute

Complete!
```

## Error Handling Strategy

### Validation Failures
1. Capture compilation errors from Roslyn
2. Re-prompt OpenAI with error details
3. Retry up to MaxRetries (default: 3)
4. Fail if still invalid after retries

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

## Configuration

### appsettings.json
```json
{
  "OpenAI": {
    "ApiKey": "API key or use env var",
    "Model": "gpt-4 | gpt-3.5-turbo"
  },
  "Engine": {
    "MaxRetries": 3
  }
}
```

### Environment Variables
- `OPENAI_API_KEY`: OpenAI API key (overrides appsettings)
- `OPENAI_MODEL`: Model selection (optional)

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

### Additional User Interactions
Extend `UserInteractionService`:
- Custom uncertainty detection
- Integration with issue tracking
- Collaborative review workflows

## Performance Considerations

### Parallel Execution Benefits
- Independent tasks execute concurrently
- Reduces total execution time
- Scales with task graph width

### Optimization Strategies
1. **Caching**: Cache OpenAI responses for identical prompts
2. **Batching**: Group similar tasks for efficient processing
3. **Lazy Loading**: Load references only when needed
4. **Connection Pooling**: Reuse HTTP connections to OpenAI

## Security Considerations

1. **API Key Management**: Store in environment variables or secure vaults
2. **Code Injection**: Validate all generated code before execution
3. **Dependency Validation**: Ensure all assembly references are trusted
4. **User Input**: Sanitize user clarifications before including in prompts
5. **Output Sanitization**: Review generated code for security vulnerabilities

## Testing Strategy

### Unit Tests
- Model validation (TaskNode, ValidationResult)
- Service isolation (CodeValidator, CodeMerger)
- Engine logic (TopologicalSort, dependency resolution)

### Integration Tests
- OpenAI API integration
- End-to-end workflow
- Error handling scenarios

### Manual Testing
- User interaction flows
- Complex task decompositions
- Validation retry logic
