# Integration Services

This folder contains services responsible for merging code from multiple tasks, handling conflicts, automatic fixing, and user interaction during the integration phase.

## Components

### CodeMergerService (Partial Class)

Merges code snippets from multiple tasks with advanced integration capabilities.

| File | Responsibility |
|------|----------------|
| `CodeMergerService.cs` | Basic merge operations and contract validation |
| `CodeMergerService.Integration.cs` | Advanced Parse → Analyze → Fix → Emit pipeline |

**Integration Pipeline:**
1. **Parse** - Parse all code snippets into Roslyn syntax trees
2. **Analyze** - Build Type Registry and detect conflicts
3. **Fix** - Apply conflict resolution strategies
4. **Emit** - Generate deduplicated merged code

**Conflict Resolution Strategies:**
- `KeepFirst` - Keep first definition, discard duplicates
- `MergeAsPartial` - Convert to partial classes
- `RemoveDuplicate` - Remove conflicting members
- `FailFast` - Fail for unresolvable conflicts

### IntegrationFixer

Provides Roslyn-based auto-fix capabilities for common integration errors.

**Supported Auto-Fixes:**
| Error Code | Description | Fix Strategy |
|------------|-------------|--------------|
| CS0246 | Missing type | Add using statement |
| CS0234 | Missing namespace | Add using statement |
| CS0104 | Ambiguous reference | Fully qualify type |
| CS0111 | Duplicate member | Remove duplicate |
| CS0101 | Duplicate type | Remove duplicate |

### AutoFixService

Provides compiler-driven patching loop for common integration errors.

**Key Methods:**
- `TryAutoFixAsync()` - Main auto-fix loop with retry logic
- `TryFixMissingInterfaceMember()` - Adds stub implementations
- `TryFixMissingAbstractMember()` - Adds override stubs
- `TryFixSealedInheritance()` - Converts inheritance to composition
- `TryFixAmbiguousReference()` - Fully qualifies type names

### IntegrationCheckpointHandler

Handles manual checkpoints for non-trivial conflicts during code integration.

**Key Methods:**
- `GenerateConflictReports()` - Creates detailed conflict reports with diffs
- `PromptForResolutionAsync()` - Interactive conflict resolution
- `RequiresManualIntervention()` - Determines if human review is needed

### TaskComplexityAnalyzer (Partial Class)

Analyzes task complexity to determine if decomposition is needed.

| File | Responsibility |
|------|----------------|
| `TaskComplexityAnalyzer.cs` | Complexity analysis logic |
| `TaskComplexityAnalyzer.Estimation.cs` | Line count and complexity estimation |

**Complexity Factors:**
- Estimated line count (threshold: 300 lines)
- Expected type count
- Method count estimation
- Dependency count
- Complexity score (0-100)

### AutoDecomposer (Partial Class)

Automatically decomposes complex tasks into smaller subtasks.

| File | Responsibility |
|------|----------------|
| `AutoDecomposer.cs` | Main decomposition logic |
| `AutoDecomposer.PartialClass.cs` | Partial class decomposition support |

**Decomposition Strategies:**
- `Functional` - Multiple independent types
- `PartialClass` - Single large class split into partials
- `InterfaceBased` - Interface + implementation
- `LayerBased` - Multi-layer service decomposition

### UserInteractionService (Partial Class)

Handles user interactions for clarifications and confirmations.

| File | Responsibility |
|------|----------------|
| `UserInteractionService.cs` | Prompt and clarification methods |
| `UserInteractionService.UncertaintyDetection.cs` | Uncertainty detection logic |

**Key Methods:**
- `HandleTaskUncertaintyAsync()` - Detects and resolves vague terms
- `ReviewTasksWithUserAsync()` - Task breakdown review and confirmation
- `AskForClarificationAsync()` - Interactive user prompting

## Design Principles

- All files are kept under 300 lines for maintainability
- Large classes use partial class pattern to split functionality
- Integration services prioritize automated fixes before manual intervention
- User interaction is minimized but available for complex conflicts
