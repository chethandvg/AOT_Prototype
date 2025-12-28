# AI Services

This folder contains services responsible for interacting with AI providers (OpenAI) for code generation, task decomposition, documentation synthesis, and intelligent prompting.

## Components

### OpenAIService (Partial Class)

The main service for OpenAI API interactions, split across multiple files for maintainability:

| File | Responsibility |
|------|----------------|
| `OpenAIService.cs` | Core fields, constructor, Responses API handling, task decomposition |
| `OpenAIService.CodeGeneration.cs` | Code generation and regeneration methods with response chaining |
| `OpenAIService.Prompts.cs` | Prompt generation and templating |
| `OpenAIService.ContractExtraction.cs` | Type contract extraction from generated code |
| `OpenAIService.ContractAware.cs` | Contract-aware code generation with frozen contracts and chaining |
| `OpenAIService.PackageVersions.cs` | NuGet package version queries |
| `OpenAIService.Documentation.cs` | Documentation and summary generation |
| `OpenAIService.TaskDecomposition.cs` | Complex task decomposition logic |

### OpenAI Responses API Integration

**Model:** `gpt-5.1-codex`  
**Endpoint:** `/v1/responses` (replaces `/v1/chat/completions`)

**Key Features:**
- **Response Storage**: All responses are stored server-side (`store: true`) for later retrieval
- **Response Chaining**: Subsequent requests can reference previous responses via `previous_response_id`
- **Context Continuity**: Maintains conversation context across multiple related code generation tasks

**Response Chaining Strategy:**
```
Initial Task (task1)
    ?
[Generate] ? Response ID: resp_1
    ?
Dependent Task (task2)
    ?
[Generate with previous_response_id: resp_1] ? Response ID: resp_2
    ?
Error in task2
    ?
[Regenerate with previous_response_id: resp_2] ? Response ID: resp_3
```

**Benefits:**
- Dependent tasks automatically inherit context from their dependencies
- Error corrections maintain awareness of original generation intent
- Reduces type mismatches and improves code coherence across tasks
- Model can build upon previous outputs rather than starting fresh

**Configuration:**
- **Timeout**: 300 seconds (to accommodate complex code generation)
- **Max Retries**: 3 attempts with exponential backoff
- **Base URL**: `https://api.openai.com/v1/`

### PromptContextBuilder

Builds enhanced prompt context with frozen contracts, known symbols, and guardrails for optimal code generation.

**Key Methods:**
- `BuildCodeGenerationContext()` - Injects contracts and guardrails
- `BuildInterfaceImplementationContext()` - Lists required methods with exact signatures
- `BuildAbstractClassImplementationContext()` - Lists abstract methods to override
- `BuildEnumUsageContext()` - Lists valid enum members
- `ValidateAgainstContracts()` - Checks for contract violations

### KnownPackageVersions

Provides a static registry of known stable package versions for .NET 9, used as a fallback when AI-based version resolution is unavailable.

## API Response Models

### Codex Response Structure
```csharp
// Response from /v1/responses endpoint
{
    "id": "resp_abc123",           // Unique response ID for chaining
    "output": [
        {
            "type": "message",
            "role": "assistant",
            "content": [
                {
                    "type": "output_text",
                    "text": "generated code..."
                }
            ]
        }
    ]
}
```

### Request Structure
```csharp
// Request to /v1/responses endpoint
{
    "model": "gpt-5.1-codex",
    "store": true,                          // Enable server-side storage
    "previous_response_id": "resp_abc123",  // Optional: chain from previous response
    "input": [
        {
            "role": "system",
            "content": "system prompt..."
        },
        {
            "role": "user",
            "content": "user request..."
        }
    ]
}
```

## Response Chain Management

The `_responseChain` dictionary tracks response IDs by task ID:

```csharp
Dictionary<string, string> _responseChain
// Key: Task ID (e.g., "task1", "task2")
// Value: Response ID (e.g., "resp_abc123")
```

**Usage Patterns:**

1. **Initial Generation**: Chain from last dependency's response
2. **Regeneration**: Chain from task's own previous generation
3. **Contract Validation**: Chain through violation feedback iterations

## Design Principles

- All files are kept under 300 lines for maintainability
- Large classes use partial class pattern to split functionality
- Services are stateless where possible, with configuration injected via constructor
- Error handling includes retry logic with exponential backoff for API calls
- Response chaining provides implicit context transfer between related tasks
- HttpClient is static to avoid socket exhaustion with 300-second timeout for complex operations

## Migration Notes

**Previous Implementation:**
- Used `/v1/chat/completions` endpoint
- No response storage or chaining
- Each request was independent

**Current Implementation:**
- Uses `/v1/responses` endpoint
- Responses are stored server-side
- Chaining via `previous_response_id` for context continuity
- Returns `(responseId, text)` tuples for future chaining

This architecture significantly improves code quality for dependent tasks and error correction scenarios by maintaining contextual awareness across the generation workflow.
