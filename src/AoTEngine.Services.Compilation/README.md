# Compilation Services

This folder contains services responsible for project building, assembly management, and per-atom compilation using Roslyn.

## Components

### ProjectBuildService (Partial Class)

Creates and builds .NET projects from generated code with dynamic package resolution.

| File | Responsibility |
|------|----------------|
| `ProjectBuildService.cs` | Core fields, constructors, `CreateProjectFromTasksAsync()` |
| `ProjectBuildService.PackageManagement.cs` | Package extraction and NuGet management |
| `ProjectBuildService.FileOperations.cs` | File saving and entry point creation |
| `ProjectBuildService.BuildValidation.cs` | Build, restore, and validation methods |

**Key Features:**
- Dynamic package version resolution via OpenAI
- Fallback to known stable versions
- Support for multiple target frameworks
- Automatic entry point generation

### AssemblyReferenceManager

Manages assembly references for Roslyn compilation, including:
- Base class library references
- NuGet package assembly resolution
- Framework assembly discovery
- Reference caching for performance

### AtomCompilationService

Performs fast Roslyn compilation per file with classified diagnostics for early failure detection.

**Key Methods:**
- `CompileAtom()` - Fast Roslyn compile per generated file
- `ClassifyDiagnostic()` - Classifies errors (SymbolCollision, MissingInterfaceMember, SignatureMismatch, etc.)
- `ValidateAgainstContracts()` - Validates generated code against frozen contracts
- `GenerateCompilationSummary()` - Human-readable error summary for LLM feedback

**Diagnostic Classifications:**
- `SymbolCollision` - Duplicate type definitions
- `MissingInterfaceMember` - Unimplemented interface members
- `SignatureMismatch` - Method signature inconsistencies
- `MissingEnumMember` - Invalid enum member usage
- `SealedInheritance` - Attempt to inherit from sealed class

## Design Principles

- All files are kept under 300 lines for maintainability
- Large classes use partial class pattern to split functionality
- Compilation services cache results where possible for performance
- Error messages are formatted to be actionable by both humans and LLMs
