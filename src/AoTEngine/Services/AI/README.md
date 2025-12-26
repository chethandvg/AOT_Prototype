# AI Services

This folder contains services responsible for interacting with AI providers (OpenAI) for code generation, task decomposition, documentation synthesis, and intelligent prompting.

## Components

### OpenAIService (Partial Class)

The main service for OpenAI API interactions, split across multiple files for maintainability:

| File | Responsibility |
|------|----------------|
| `OpenAIService.cs` | Core fields, constructor, initial task decomposition |
| `OpenAIService.CodeGeneration.cs` | Code generation and regeneration methods |
| `OpenAIService.Prompts.cs` | Prompt generation and templating |
| `OpenAIService.ContractExtraction.cs` | Type contract extraction from generated code |
| `OpenAIService.ContractAware.cs` | Contract-aware code generation with violation feedback |
| `OpenAIService.PackageVersions.cs` | NuGet package version queries |
| `OpenAIService.Documentation.cs` | Documentation and summary generation |
| `OpenAIService.TaskDecomposition.cs` | Complex task decomposition logic |

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

## Design Principles

- All files are kept under 300 lines for maintainability
- Large classes use partial class pattern to split functionality
- Services are stateless where possible, with configuration injected via constructor
- Error handling includes retry logic with exponential backoff for API calls
