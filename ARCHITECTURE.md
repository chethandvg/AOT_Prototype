# AoT Engine Architecture

## System Overview

The AoT (Atom of Thought) Engine is designed around six core principles:
1. **Decomposition**: Break complex tasks into atomic units
2. **Parallelism**: Execute independent tasks concurrently
3. **Validation**: Ensure code quality through compilation and linting
4. **Interaction**: Engage users when uncertainties arise
5. **Documentation**: Generate structured documentation and training data
6. **Complexity Management**: Ensure tasks stay within manageable size limits (≤300 lines)

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
                        │
                        ▼
              ┌─────────────────────┐
              │  Documentation      │
              │  Service (NEW)      │
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
              │  Analyzer (NEW)     │
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
- **NEW**: Summary, SummaryModel, ValidationAttemptCount, SummaryGeneratedAtUtc
- Tracks: Completion status, retry count, validation errors

**ComplexityMetrics** (NEW)
- Task complexity analysis results
- Properties: EstimatedLineCount, ExpectedTypeCount, DependencyCount
- EstimatedMethodCount, ComplexityScore (0-100), RequiresDecomposition
- RecommendedSubtaskCount, MaxLineThreshold (default: 300)
- Used to determine if task needs splitting

**TaskDecompositionStrategy** (NEW)
- Decomposition strategy for complex tasks
- Types: Functional, PartialClass, InterfaceBased, LayerBased
- Properties: OriginalTaskId, Subtasks, PartialClassConfig
- Manages shared state and dependencies between subtasks

**TaskSummaryRecord** (NEW)
- Structured summary for each task
- Properties: TaskId, TaskDescription, Purpose, KeyBehaviors, EdgeCases
- ValidationNotes, GeneratedCodeHash, SummaryModel, CreatedUtc
- Used for documentation and training data export

**ProjectDocumentation** (NEW)
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

GenerateTaskSummaryAsync() // NEW
  ├─> Analyzes generated code
  ├─> Generates structured JSON summary
  └─> Returns TaskSummaryInfo

GenerateArchitectureSummaryAsync() // NEW
  ├─> Analyzes all task summaries
  ├─> Generates high-level overview
  └─> Returns markdown summary

DecomposeComplexTaskAsync() // NEW
  ├─> Receives task estimated to exceed 300 lines
  ├─> Creates subtask breakdown with OpenAI
  ├─> Manages dependencies between subtasks
  └─> Returns list of smaller TaskNodes
```

**TaskComplexityAnalyzer** (NEW)
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

**DocumentationService** (NEW)
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
  ├─> Step 1.5: Complexity analysis (NEW)
  │   ├─> Analyze each task for complexity
  │   ├─> Decompose tasks >300 lines
  │   └─> Update task list with subtasks
  ├─> Step 2: Execute tasks
  │   └─> Parallel execution with validation
  ├─> Step 3: Validate contracts
  ├─> Step 4: Merge code
  ├─> Step 5: Generate report
  ├─> Step 6: Synthesize documentation (NEW)
  │   ├─> Generate project documentation
  │   ├─> Export markdown
  │   ├─> Export JSON
  │   └─> Export JSONL
  └─> Return AoTResult with documentation
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
Complexity Analysis (NEW)
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
    ├─> Generate Code (OpenAI)
    ├─> Validate Code (Roslyn)
    ├─> Track Validation Attempts (NEW)
    ├─> Generate Task Summary (NEW)
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
Documentation Synthesis (NEW)
    ├─> Project Documentation
    ├─> Architecture Summary
    └─> Training Dataset
    │
    ▼
Output (Final Code + Report + Documentation)
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

### Documentation Failures (NEW)
1. Catch exceptions during summary generation
2. Fall back to minimal summary
3. Log warning but don't fail task
4. Continue with code generation
5. Handle file I/O errors gracefully

## Configuration

### appsettings.json
```json
{
  "OpenAI": {
    "ApiKey": "API key or use env var",
    "Model": "gpt-4 | gpt-3.5-turbo"
  },
  "Engine": {
    "MaxRetries": 3,
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
