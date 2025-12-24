# AOT_Prototype
**Atom of Thought (AoT) Engine** - A C# implementation of an intelligent code generation system

## Overview

The AoT Engine is a sophisticated C# application that leverages OpenAI's GPT models to decompose complex programming tasks into atomic subtasks, execute them in parallel, validate the generated code, and merge the results into a complete solution.

## 📚 Documentation

- **[README.md](README.md)** - This file: Project overview and quick start guide
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Detailed system architecture and technical design
- **[USAGE.md](USAGE.md)** - Comprehensive usage examples and scenarios
- **[CHANGELOG.md](CHANGELOG.md)** - Complete feature reference and version history

## Features

### 1. **Task Decomposition**
- Uses OpenAI to decompose complex requests into atomic subtasks
- Creates a Directed Acyclic Graph (DAG) of dependencies
- Identifies parallel execution opportunities

### 2. **Parallel Execution**
- Executes independent tasks concurrently using `async`/`await` and `Task.WhenAll`
- Respects task dependencies
- Feeds only required context to each task

### 3. **Code Validation**
- Compiles generated code snippets using Roslyn (Microsoft.CodeAnalysis)
- Runs linting checks for code quality
- Automatically retries with error feedback on validation failures

### 4. **Interactive Uncertainty Handling** ⚠️ NEW
- Detects uncertainties in task descriptions
- Prompts user for clarification when needed
- Allows review and confirmation of task decomposition
- Supports user choices for ambiguous requirements

### 5. **Code Integration**
- Validates contracts between code snippets
- Merges all snippets into a cohesive solution
- Generates execution reports

## Architecture

```
AoTEngine/
├── Models/                     # Data models
│   ├── TaskNode.cs            # Represents atomic task in DAG
│   ├── TaskDecompositionRequest.cs
│   ├── TaskDecompositionResponse.cs
│   └── ValidationResult.cs
├── Services/                   # Core services
│   ├── OpenAIService.cs       # OpenAI API integration
│   ├── CodeValidatorService.cs # Code compilation & validation
│   ├── CodeMergerService.cs   # Code merging & contract validation
│   └── UserInteractionService.cs # Handles user input for uncertainties
├── Core/                       # Engine components
│   ├── ParallelExecutionEngine.cs # Parallel task execution
│   └── AoTEngineOrchestrator.cs   # Main workflow orchestrator
└── Program.cs                  # Entry point
```

## Prerequisites

- .NET 9.0 SDK or later
- OpenAI API Key

## Installation

1. Clone the repository:
```bash
git clone https://github.com/chethandvg/AOT_Prototype.git
cd AOT_Prototype
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Configure OpenAI API Key:

**Option 1:** Update `appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "your-api-key-here",
    "Model": "gpt-4"
  }
}
```

**Option 2:** Set environment variable:
```bash
export OPENAI_API_KEY="your-api-key-here"
```

## Usage

### Running the Engine

```bash
cd src/AoTEngine
dotnet run
```

### Example Workflow

1. **Enter your request:**
```
Enter your coding request: Create a REST API for a todo list application with authentication
```

2. **Review task decomposition:**
The engine will decompose your request and ask for confirmation:
```
Task Decomposition Review
═══════════════════════════════════════════════════════════
Task task1: Create authentication service
  Dependencies: None

Task task2: Create todo item model
  Dependencies: None

Task task3: Create todo API controller
  Dependencies: task1, task2

Does this task decomposition look correct? (y/n):
```

3. **Handle uncertainties:**
If the engine detects vague requirements, it will ask for clarification:
```
⚠️  UNCERTAINTY DETECTED - User Input Required
═══════════════════════════════════════════════════════════
Context: Task ID: task1
Description: Create authentication service

Question: What security requirements are needed?
(authentication/authorization/encryption/validation)

Your response: JWT-based authentication with role-based authorization
```

4. **View results:**
The engine will execute tasks in parallel, validate code, and output:
- Execution report
- Final merged code
- Saved to `generated_code.cs`

## Building the Project

```bash
dotnet build
```

## Running Tests

```bash
dotnet test
```

## Configuration

### appsettings.json

```json
{
  "OpenAI": {
    "ApiKey": "YOUR_API_KEY",
    "Model": "gpt-4"          // or "gpt-3.5-turbo"
  },
  "Engine": {
    "MaxRetries": 3           // Max retry attempts for validation failures
  }
}
```

## How It Works

1. **Decomposition Phase**
   - User provides a high-level request
   - OpenAI decomposes it into atomic subtasks
   - Dependencies are identified
   - User reviews and confirms the decomposition

2. **Uncertainty Resolution Phase**
   - System detects vague or ambiguous terms
   - User is prompted for clarification
   - Context is enriched with clarifications

3. **Execution Phase**
   - Tasks are organized in topological order
   - Independent tasks execute in parallel
   - Each task receives only necessary context
   - Code is generated via OpenAI

4. **Validation Phase**
   - Generated code is compiled using Roslyn
   - Linting checks are performed
   - If validation fails, re-prompt with errors
   - Retry up to MaxRetries times

5. **Integration Phase**
   - Contracts between tasks are validated
   - Code snippets are merged
   - Final solution is compiled
   - Execution report is generated

## Key Technologies

- **C# / .NET 9.0**: Core framework
- **OpenAI SDK**: GPT integration for code generation
- **Roslyn (Microsoft.CodeAnalysis)**: Code compilation and validation
- **Newtonsoft.Json**: JSON serialization
- **xUnit**: Unit testing framework

## Project Structure

```
AOT_Prototype/
├── src/
│   └── AoTEngine/              # Main application
│       ├── Models/
│       ├── Services/
│       ├── Core/
│       ├── Program.cs
│       └── appsettings.json
├── tests/
│   └── AoTEngine.Tests/        # Unit tests
└── AoTEngine.sln               # Solution file
```

## Example Output

```
=== AoT Engine Execution Report ===

Total Tasks: 4
Completed Tasks: 4
Validated Tasks: 4

Task Details:
  - task1: Create authentication service
    Dependencies: None
    Status: ✓ Completed, ✓ Validated
  - task2: Create todo item model
    Dependencies: None
    Status: ✓ Completed, ✓ Validated
  - task3: Create todo API controller
    Dependencies: task1, task2
    Status: ✓ Completed, ✓ Validated
  - task4: Create unit tests
    Dependencies: task1, task2, task3
    Status: ✓ Completed, ✓ Validated

Merged Code Length: 3521 characters
Merged Code Lines: 142
```

## Limitations

- Requires valid OpenAI API key
- Code generation quality depends on OpenAI model
- Complex enterprise patterns may need manual refinement
- Test execution is simulated (actual test running TBD)

## Future Enhancements

- [ ] Actual unit test execution
- [ ] Support for multiple programming languages
- [ ] Integration with CI/CD pipelines
- [ ] Advanced dependency resolution
- [ ] Code optimization suggestions
- [ ] Support for custom code templates
- [ ] Integration with version control systems

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues.

## License

See LICENSE file for details.

## Acknowledgments

Built with inspiration from the "Atom of Thought" concept, emphasizing decomposition of complex problems into atomic, manageable units.

