# AoT Engine Workflow - Coordinating Atomic Code Generation Tasks

This project implements the "Coordinating Atomic Code Generation Tasks" workflow pattern, which provides a structured approach to AI-assisted code generation through planning, clarification, and iterative verification.

## Overview

The workflow implements the following key principles:

### 1. Planning First
Before coding, use a powerful LLM to clarify requirements and outline the solution:
- Ask clarifying questions to understand requirements
- Generate a detailed specification document
- Create a "blueprint" of small, logical tasks

### 2. Gather Uncertainties Up Front
Use the LLM to identify unknowns and produce a complete spec:
- Detect ambiguous requirements
- Prompt for missing technical specifications
- Clarify edge cases and integration points

### 3. Break Work into Small Chunks
Turn the spec into atomic subtasks:
- Each task generates ≤300 lines of code
- Tasks are independently compilable
- Clear dependencies between tasks

### 4. Iterate and Verify
After each atomic task, verify its output:
- Immediate compilation check after generation
- Automatic retry with error feedback
- Checkpoints for progress tracking

### 5. Shared Context (Blackboard Pattern)
Maintain a global context store for all tasks:
- Code artifacts from completed tasks
- Type definitions registry
- Clarified requirements
- Intermediate results

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         User Request                             │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│              CoordinatedWorkflowOrchestrator                     │
└─────────────────────────┬───────────────────────────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
┌───────────────┐  ┌───────────────┐  ┌───────────────┐
│   Planning    │  │    Shared     │  │   Execution   │
│   Service     │  │    Context    │  │    Engine     │
│               │  │  (Blackboard) │  │               │
│ • Clarify     │  │               │  │ • Generate    │
│ • Specify     │  │ • Artifacts   │  │ • Validate    │
│ • Blueprint   │  │ • Types       │  │ • Verify      │
└───────────────┘  │ • Results     │  └───────────────┘
                   └───────────────┘
```

## Workflow Phases

### Phase 1: Requirements Gathering
- Analyzes the user request for uncertainties
- Generates clarifying questions
- Collects user responses

### Phase 2: Specification Generation
- Creates a structured specification document
- Includes functional and non-functional requirements
- Defines constraints and assumptions

### Phase 3: Blueprint Creation
- Breaks specification into atomic tasks
- Estimates lines of code per task
- Defines task dependencies

### Phase 4: Task Conversion
- Converts blueprint tasks to executable TaskNodes
- Injects context from shared blackboard

### Phase 5: Contract-First Generation (Optional)
- Generates frozen interface contracts
- Creates enum and model contracts
- Prevents API drift during implementation

### Phase 6: Task Execution with Verification
- Executes tasks respecting dependencies
- Verifies each task immediately after generation
- Updates shared context with results

### Phase 7-9: Validation, Merge, and Report
- Validates all contracts
- Merges code snippets
- Generates comprehensive execution report

## Usage

### Running the Workflow

```bash
cd src/AoTEngine.Workflow
dotnet run
```

### Configuration

Edit `appsettings.json` to configure the workflow:

```json
{
  "OpenAI": {
    "ApiKey": "YOUR_API_KEY",
    "PlanningModel": "gpt-5.1"
  },
  "Workflow": {
    "EnablePlanningPhase": true,
    "EnableClarificationPhase": true,
    "EnableBlueprintGeneration": true,
    "EnableIterativeVerification": true,
    "MaxLinesPerTask": 300,
    "EnableContractFirst": true,
    "MaxRetries": 3
  }
}
```

### Workflow Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `EnablePlanningPhase` | `true` | Enable the planning phase (clarification + specification) |
| `EnableClarificationPhase` | `true` | Enable uncertainty detection and clarification prompts |
| `EnableBlueprintGeneration` | `true` | Enable blueprint creation from specification |
| `EnableIterativeVerification` | `true` | Verify each task immediately after generation |
| `MaxLinesPerTask` | `300` | Maximum lines of code per generated task |
| `EnableContractFirst` | `true` | Generate frozen contracts before implementation |
| `MaxRetries` | `3` | Maximum retry attempts for failed tasks |

## Shared Context (Blackboard)

The `SharedContext` class maintains global state accessible to all tasks:

```csharp
public class SharedContext
{
    // Original request and clarifications
    public string OriginalRequest { get; set; }
    public List<ClarifiedRequirement> ClarifiedRequirements { get; set; }
    
    // Generated artifacts
    public SpecificationDocument? Specification { get; set; }
    public ProjectBlueprint? Blueprint { get; set; }
    
    // Code artifacts and types
    public Dictionary<string, CodeArtifact> CodeArtifacts { get; set; }
    public Dictionary<string, TypeDefinition> TypeRegistry { get; set; }
    
    // Task results
    public Dictionary<string, TaskResult> IntermediateResults { get; set; }
    
    // Progress tracking
    public List<WorkflowCheckpoint> Checkpoints { get; set; }
}
```

## Output Files

The workflow generates the following files in the output directory:

| File | Description |
|------|-------------|
| `generated_code.cs` | Final merged C# code |
| `workflow_context.json` | Complete shared context as JSON |
| `execution_report.txt` | Detailed execution report |

## Example Session

```
╔═══════════════════════════════════════════════════════════╗
║     AoT Engine - Coordinated Atomic Code Generation       ║
╚═══════════════════════════════════════════════════════════╝

Enter your coding request: Create a REST API for user management

═══════════════════════════════════════════════════════════
📋 PHASE 1: Gathering Requirements & Clarifying Uncertainties
═══════════════════════════════════════════════════════════

📌 Identified 3 areas that need clarification:

Q: What authentication method should be used?
A: JWT tokens

Q: What database should be used?
A: PostgreSQL

Q: Should unit tests be included?
A: Yes

═══════════════════════════════════════════════════════════
📝 PHASE 2: Generating Specification Document
═══════════════════════════════════════════════════════════

📄 Specification Summary:
   Title: User Management REST API
   Functional Requirements: 5
   ...

═══════════════════════════════════════════════════════════
🗺️  PHASE 3: Creating Project Blueprint
═══════════════════════════════════════════════════════════

📋 Blueprint: UserManagement
   Tasks: 6
   ...

✅ Workflow Completed Successfully
```

## Key Benefits

1. **Reduced Hallucinations**: By gathering all requirements upfront and using a shared context, the LLM has complete information for each task.

2. **Early Error Detection**: Iterative verification catches errors immediately, preventing cascading failures.

3. **Coordinated Tasks**: The blackboard pattern ensures all tasks share knowledge and don't duplicate definitions.

4. **Human-in-the-Loop**: The planning phase engages the user to clarify requirements, ensuring alignment.

5. **Checkpoint Recovery**: Progress is tracked at each phase, enabling future recovery features.

## See Also

- [ARCHITECTURE.md](../../ARCHITECTURE.md) - Overall system architecture
- [MODULAR_ARCHITECTURE.md](../../MODULAR_ARCHITECTURE.md) - Module design guide
- [USAGE.md](../../USAGE.md) - Usage examples
