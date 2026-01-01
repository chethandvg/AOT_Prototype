# Getting Started with AoTEngine.AtomicAgent

This guide will help you get started with the **Atomic Thought Framework** implementation.

## Overview

The AtomicAgent is an autonomous coding agent that:
- Breaks down coding tasks into **atomic units** (atoms)
- Orders them using **topological sort** (handles dependencies correctly)
- Generates code using **tiered context injection** (saves ~70% tokens)
- Validates using **in-memory Roslyn compilation**
- Self-corrects through a **feedback loop** (compile errors → LLM → fix)

## Prerequisites

- **.NET 9.0 SDK** or later
- **OpenAI API Key** with access to GPT-4 or GPT-5.1
- **Windows, macOS, or Linux**

## Installation

1. **Clone the repository**:
   ```bash
   git clone https://github.com/chethandvg/AOT_Prototype.git
   cd AOT_Prototype
   ```

2. **Navigate to the AtomicAgent project**:
   ```bash
   cd src/AoTEngine.AtomicAgent
   ```

3. **Set up your API key**:
   ```bash
   cp .env.example .env
   ```
   
   Edit `.env` and add your OpenAI API key:
   ```bash
   OPENAI_API_KEY=sk-your-actual-api-key-here
   OPENAI_MODEL=gpt-4
   ```

4. **Build the project**:
   ```bash
   dotnet build
   ```

## Running Your First Example

1. **Start the application**:
   ```bash
   dotnet run
   ```

2. **You'll see the banner**:
   ```
   ╔════════════════════════════════════════════════════════════════╗
   ║   Atomic Thought Framework - Autonomous C# Coding Agent       ║
   ║   Implementing the 8-Component Architectural Blueprint         ║
   ╚════════════════════════════════════════════════════════════════╝
   
   Components:
     1. ✓ Workspace Service (Sandboxed File System)
     2. ✓ Blackboard Service (Shared Knowledge Base)
     3. ✓ Planner Agent (Abstractions First + Topological Sort)
     4. ✓ Clarification Loop (Ambiguity Detection)
     5. ✓ Context Engine (Tiered Injection + Hot Cache)
     6. ✓ Atomic Worker Agent (Code Generation)
     7. ✓ Roslyn Feedback Loop (In-Memory Compilation)
     8. ⚠  Token Garbage Collection (Not yet implemented)
   ```

3. **Enter a coding request or press Enter for demo**:
   ```
   Enter your coding request:
   > 
   ```
   
   Press Enter to use the default demo request, or type your own like:
   ```
   Create a simple calculator class with add, subtract, multiply, and divide methods
   ```

4. **The system will process your request through 4 phases**:
   - **Phase 1**: Clarification (detects ambiguities)
   - **Phase 2**: Planning (generates atoms in dependency order)
   - **Phase 3**: Execution (generates and validates code)
   - **Phase 4**: Results (saves files and manifest)

## Understanding the Output

### Workspace Directory

By default, generated code is saved to `./GeneratedProjects/`. You can specify a different directory when prompted.

### Generated Files

```
./GeneratedProjects/
├── src/
│   ├── Core/
│   │   ├── dtos/
│   │   │   └── UserDto.cs
│   │   └── interfaces/
│   │       └── IUserRepository.cs
│   └── Infrastructure/
│       └── implementations/
│           └── FileUserRepository.cs
└── solution_manifest.json
```

### Solution Manifest

The `solution_manifest.json` file contains:
- **Project metadata**: Name, namespace, framework
- **Project hierarchy**: Layer definitions and dependency rules
- **Semantic symbol table**: Interface and DTO signatures
- **Atoms**: List of all atomic units with their status

Example:
```json
{
  "project_metadata": {
    "name": "AtomicAgentPrototype",
    "root_namespace": "AtomicAgent",
    "target_framework": "net9.0"
  },
  "atoms": [
    {
      "id": "atom_001",
      "type": "dto",
      "name": "UserDto",
      "layer": "Core",
      "status": "completed",
      "dependencies": [],
      "file_path": "src/Core/dtos/UserDto.cs"
    }
  ]
}
```

## Example Scenarios

### Scenario 1: Simple CRUD Application

**Request**:
```
Create a task management system with:
- Task DTO with Id, Title, Description, DueDate
- ITaskRepository interface with CRUD methods
- InMemoryTaskRepository implementation
```

**What happens**:
1. System generates 3 atoms (DTO → Interface → Implementation)
2. Validates dependency order automatically
3. Each atom is compiled and validated before proceeding
4. Output: 3 C# files in proper Clean Architecture structure

### Scenario 2: Multi-Layer Application

**Request**:
```
Create a user authentication system with:
- User and Credential DTOs
- IAuthService interface
- JwtAuthService implementation
- AuthController for API endpoints
```

**What happens**:
1. System generates ~6-8 atoms across 3 layers
2. Core: DTOs and IAuthService interface
3. Infrastructure: JwtAuthService implementation
4. Presentation: AuthController
5. Validates architectural constraints (Core can't depend on Infrastructure)

### Scenario 3: Handling Ambiguity

**Request**:
```
Build a system to manage things
```

**What happens**:
```
⚠️  UNCERTAINTY DETECTED - Clarification Required
═══════════════════════════════════════════════════
Please clarify the following:
1. What type of storage? (database/file/memory/none)
   Storage: memory
2. What type of interface? (console/api/web/none)
   Interface: api
3. Any specific frameworks or libraries to use? (or 'none')
   Frameworks: ASP.NET Core
```

System enriches the request with your clarifications and proceeds.

## Configuration Options

Edit `appsettings.json` to customize behavior:

### Workspace Configuration
```json
"Workspace": {
  "RootPath": "./output",
  "EnableSandboxing": true  // Prevents writing outside workspace
}
```

### Planner Configuration
```json
"Planner": {
  "AbstractionsFirst": true,        // DTOs → Interfaces → Implementations
  "EnableTopologicalSort": true,    // Uses Kahn's Algorithm
  "MaxRetryOnCircularDependency": 3 // Retry count for cycles
}
```

### Context Engine Configuration
```json
"Context": {
  "EnableTieredInjection": true,       // Use 3-tier context
  "EnableHotCache": true,              // Cache signatures in memory
  "CacheSlidingExpirationMinutes": 30, // Cache expiration
  "MaxTokens": 128000,                 // Model context window
  "TokenGarbageCollectionThreshold": 100000
}
```

### Execution Configuration
```json
"Execution": {
  "MaxRetries": 3,              // Retry count per atom
  "EnableRoslynFeedback": true, // Use compilation errors for self-correction
  "AutoFixAttempts": 3          // Auto-fix attempts
}
```

### Roslyn Configuration
```json
"Roslyn": {
  "EnableInMemoryCompilation": true, // Compile in memory (fast)
  "EnableSemanticExtraction": true,  // Extract signatures to symbol table
  "SuppressWarnings": true           // Only show errors, not warnings
}
```

## Troubleshooting

### Issue: "OpenAI API key not found"

**Solution**: Ensure your `.env` file or `appsettings.json` contains a valid API key:
```json
"OpenAI": {
  "ApiKey": "sk-your-key-here"
}
```

### Issue: "Circular dependency detected"

**Solution**: The system will automatically retry up to 3 times. If it persists:
1. Simplify your request
2. Be more explicit about dependencies
3. Check the logs for which atoms are involved

### Issue: "Compilation failed after 3 attempts"

**Solution**: 
1. Check the error messages in the console
2. The atom is marked as "failed" in the manifest
3. Review the generated code in the workspace
4. You can manually fix and re-run

### Issue: "Architectural violation: Core atom cannot have dependencies"

**Solution**: This is by design! Core layer should have zero dependencies. The system prevents violations of Clean Architecture principles.

## Advanced Usage

### Custom Output Directory

When prompted, specify a custom directory:
```
📁 Enter output directory for generated code and project files:
   (Press Enter to use default: './GeneratedProjects')
/path/to/my/custom/output
```

### Understanding Atom Types

- **dto**: Data Transfer Objects (no logic, just properties)
- **interface**: Interface definitions (no implementation)
- **implementation**: Classes implementing interfaces
- **test**: Unit test classes (future enhancement)

### Understanding Atom Status

- **pending**: Not yet executed
- **in_progress**: Currently being generated
- **review**: Code generated, awaiting Roslyn validation
- **completed**: Successfully compiled and validated
- **failed**: Failed after maximum retry attempts

## Next Steps

1. **Try the examples** above to understand the workflow
2. **Experiment** with different types of requests
3. **Review** the generated `solution_manifest.json` to understand state tracking
4. **Read** [ATOMIC_AGENT_IMPLEMENTATION_SUMMARY.md](../../ATOMIC_AGENT_IMPLEMENTATION_SUMMARY.md) for architectural details
5. **Compare** with the original AoTEngine in `src/AoTEngine/`

## Resources

- **Architecture**: [ATOMIC_AGENT_IMPLEMENTATION_SUMMARY.md](../../ATOMIC_AGENT_IMPLEMENTATION_SUMMARY.md)
- **Project README**: [README.md](README.md)
- **Main Repository README**: [../../README.md](../../README.md)
- **API Reference**: See inline XML documentation in source files

## Getting Help

If you encounter issues:
1. Check the console output for detailed error messages
2. Review the `solution_manifest.json` for atom statuses
3. Enable verbose logging by setting `LogLevel.Default` to `Debug` in `appsettings.json`
4. Check the [Issues](https://github.com/chethandvg/AOT_Prototype/issues) page

## Contributing

Contributions are welcome! See the main repository for contribution guidelines.
