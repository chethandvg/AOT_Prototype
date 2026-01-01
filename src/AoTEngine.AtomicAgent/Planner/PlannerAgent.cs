using AoTEngine.AtomicAgent.Models;
using AoTEngine.Services;
using Microsoft.Extensions.Logging;

namespace AoTEngine.AtomicAgent.Planner;

/// <summary>
/// The Planner Agent implements the "Abstractions First" strategy.
/// Section 5 of the architectural blueprint.
/// </summary>
public class PlannerAgent
{
    private readonly OpenAIService _openAIService;
    private readonly ILogger<PlannerAgent> _logger;
    private readonly bool _abstractionsFirst;
    private readonly bool _enableTopologicalSort;
    private readonly int _maxRetryOnCircularDependency;

    public PlannerAgent(
        OpenAIService openAIService,
        ILogger<PlannerAgent> logger,
        bool abstractionsFirst = true,
        bool enableTopologicalSort = true,
        int maxRetryOnCircularDependency = 3)
    {
        _openAIService = openAIService;
        _logger = logger;
        _abstractionsFirst = abstractionsFirst;
        _enableTopologicalSort = enableTopologicalSort;
        _maxRetryOnCircularDependency = maxRetryOnCircularDependency;
    }

    /// <summary>
    /// Generates a plan (list of atoms) from the user request.
    /// Enforces abstractions-first strategy and validates dependencies.
    /// </summary>
    public async Task<List<Atom>> GeneratePlanAsync(string userRequest, string context = "")
    {
        _logger.LogInformation("Generating plan with Abstractions First strategy");

        // Build the planning prompt
        var planningPrompt = BuildPlanningPrompt(userRequest, context);

        // Call OpenAI to decompose into atoms
        var decomposition = await _openAIService.DecomposeTaskAsync(
            new AoTEngine.Models.TaskDecompositionRequest
            {
                OriginalRequest = planningPrompt,
                Context = context
            });

        // Convert TaskNodes to Atoms
        var atoms = ConvertToAtoms(decomposition.Tasks);

        // Enforce abstractions first
        if (_abstractionsFirst)
        {
            atoms = EnforceAbstractionsFirst(atoms);
        }

        // Validate and sort dependencies
        if (_enableTopologicalSort)
        {
            for (int retry = 0; retry < _maxRetryOnCircularDependency; retry++)
            {
                try
                {
                    atoms = TopologicalSorter.Sort(atoms);
                    _logger.LogInformation("Successfully sorted {Count} atoms", atoms.Count);
                    break;
                }
                catch (CircularDependencyException ex)
                {
                    _logger.LogWarning("Circular dependency detected (attempt {Retry}): {Message}", 
                        retry + 1, ex.Message);

                    if (retry < _maxRetryOnCircularDependency - 1)
                    {
                        // Ask LLM to refactor and break the cycle
                        atoms = await RefactorToBreakCycleAsync(atoms, ex.Message);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        return atoms;
    }

    /// <summary>
    /// Builds the planning prompt with abstractions-first instructions.
    /// </summary>
    private string BuildPlanningPrompt(string userRequest, string context)
    {
        var prompt = $@"You are an expert C# Architect. Decompose the following request into atomic tasks (called ""Atoms"").

CRITICAL INSTRUCTIONS - Abstractions First Strategy and Complete Application:

1. ALWAYS CREATE A PRESENTATION LAYER WITH AN ENTRY POINT:
   - For console applications: Create a Program.cs with Main method in Presentation layer
   - For Web APIs: Create Controllers and Program.cs in Presentation layer
   - For MVC/Blazor: Create appropriate entry points and views in Presentation layer
   - The application MUST be executable - not just class libraries!

2. UNDERSTAND THE DOMAIN COMPLEXITY:
   - Analyze all entities, operations, and data flows mentioned
   - Create appropriate DTOs for all data structures
   - Define interfaces for all services and repositories
   - Consider error handling, validation, and business rules

3. Abstractions First Order:
   a. Identify all nouns (Entities/DTOs) and define them as 'dto' type atoms FIRST
   b. Identify all verbs (Capabilities) and define them as 'interface' type atoms SECOND
   c. Define 'implementation' type atoms in Infrastructure layer
   d. Define Presentation layer atoms (Program, Controllers, etc.) LAST

4. LAYER RULES:
   - Core: DTOs and Interfaces only (ZERO dependencies on other layers)
   - Infrastructure: Implementations of Core interfaces (depends on Core)
   - Presentation: Entry points, Controllers, UI (depends on Core and Infrastructure)

5. FOR THIS REQUEST, CREATE:
   - All necessary DTOs representing data structures
   - All service/repository interfaces
   - All implementations
   - A COMPLETE Presentation layer with Program.cs as entry point

Request: {userRequest}

Context: {context}

For each atom, specify:
- A unique ID (atom_001, atom_002, etc.)
- Type: 'dto', 'interface', or 'implementation'
- Name: The class/interface name
- Layer: 'Core', 'Infrastructure', or 'Presentation'
- Dependencies: List of atom IDs this depends on
- Description: What this atom implements

Example for a CSV analysis application:
Atom ID: atom_001
Type: dto
Name: CountryDataDto
Layer: Core
Dependencies: []
Description: Data transfer object for country oil production/consumption data

Atom ID: atom_002
Type: interface
Name: ICsvReaderService
Layer: Core
Dependencies: [atom_001]
Description: Interface for reading CSV files and parsing country data

Atom ID: atom_003
Type: implementation
Name: CsvReaderService
Layer: Infrastructure
Dependencies: [atom_001, atom_002]
Description: Implements CSV reading and parsing logic

Atom ID: atom_004
Type: implementation
Name: Program
Layer: Presentation
Dependencies: [atom_001, atom_002, atom_003]
Description: Console application entry point with Main method";

        return prompt;
    }

    /// <summary>
    /// Converts TaskNodes from the existing system to Atoms.
    /// </summary>
    private List<Atom> ConvertToAtoms(List<AoTEngine.Models.TaskNode> tasks)
    {
        var atoms = new List<Atom>();
        foreach (var task in tasks)
        {
            atoms.Add(new Atom
            {
                Id = task.Id,
                Name = task.Description.Split(' ').Last(), // Extract class name from description
                Type = DetermineAtomType(task.Description),
                Layer = DetermineLayer(task.Description),
                Dependencies = task.Dependencies,
                Status = AtomStatus.Pending,
                FilePath = ""
            });
        }
        return atoms;
    }

    /// <summary>
    /// Determines the atom type from task description.
    /// </summary>
    private string DetermineAtomType(string description)
    {
        var lower = description.ToLowerInvariant();
        
        if (lower.Contains("interface") || lower.Contains("contract"))
            return AtomType.Interface;
        
        if (lower.Contains("dto") || lower.Contains("model") || lower.Contains("entity"))
            return AtomType.Dto;
        
        if (lower.Contains("test"))
            return AtomType.Test;
        
        return AtomType.Implementation;
    }

    /// <summary>
    /// Determines the layer from task description.
    /// </summary>
    private string DetermineLayer(string description)
    {
        var lower = description.ToLowerInvariant();
        
        // DTOs and interfaces without dependencies always go to Core
        if (lower.Contains("dto") || lower.Contains("model") || lower.Contains("entity"))
            return "Core";
        
        if (lower.Contains("interface") && !lower.Contains("implement"))
            return "Core";
        
        // Anything with "implement", "service", "repository", "database", "file" goes to Infrastructure
        if (lower.Contains("implement") || lower.Contains("repository") || 
            lower.Contains("service") || lower.Contains("database") || 
            lower.Contains("file") || lower.Contains("storage") ||
            lower.Contains("persistence") || lower.Contains("data access"))
            return "Infrastructure";
        
        // Controllers, API, UI go to Presentation
        if (lower.Contains("controller") || lower.Contains("api") || 
            lower.Contains("ui") || lower.Contains("view") ||
            lower.Contains("endpoint") || lower.Contains("handler"))
            return "Presentation";
        
        // Default: if it looks like a contract (interface keyword), use Core
        // Otherwise use Infrastructure for safety
        if (lower.Contains("interface") || lower.Contains("abstract"))
            return "Core";
        
        return "Infrastructure"; // Changed from Core to Infrastructure as safer default
    }

    /// <summary>
    /// Enforces abstractions-first ordering: DTOs, then Interfaces, then Implementations.
    /// Also ensures Core layer atoms have no dependencies.
    /// </summary>
    private List<Atom> EnforceAbstractionsFirst(List<Atom> atoms)
    {
        var dtos = atoms.Where(a => a.Type == AtomType.Dto).ToList();
        var interfaces = atoms.Where(a => a.Type == AtomType.Interface).ToList();
        var implementations = atoms.Where(a => a.Type == AtomType.Implementation).ToList();
        var tests = atoms.Where(a => a.Type == AtomType.Test).ToList();

        // Ensure DTOs and interfaces don't have implementation dependencies
        foreach (var dto in dtos)
        {
            dto.Dependencies.RemoveAll(depId => 
                atoms.Any(a => a.Id == depId && a.Type == AtomType.Implementation));
            
            // If DTO is in Core and still has dependencies, clear them
            if (dto.Layer == "Core" && dto.Dependencies.Any())
            {
                _logger.LogWarning("Removing dependencies from Core DTO {AtomId}: {Dependencies}",
                    dto.Id, string.Join(", ", dto.Dependencies));
                dto.Dependencies.Clear();
            }
        }

        foreach (var iface in interfaces)
        {
            iface.Dependencies.RemoveAll(depId => 
                atoms.Any(a => a.Id == depId && a.Type == AtomType.Implementation));
            
            // Interfaces in Core can only depend on Core DTOs
            if (iface.Layer == "Core")
            {
                var invalidDeps = iface.Dependencies
                    .Where(depId => !atoms.Any(a => a.Id == depId && a.Type == AtomType.Dto && a.Layer == "Core"))
                    .ToList();
                
                if (invalidDeps.Any())
                {
                    _logger.LogWarning("Removing invalid dependencies from Core interface {AtomId}: {Dependencies}",
                        iface.Id, string.Join(", ", invalidDeps));
                    
                    foreach (var invalidDep in invalidDeps)
                    {
                        iface.Dependencies.Remove(invalidDep);
                    }
                }
            }
        }

        var reordered = new List<Atom>();
        reordered.AddRange(dtos);
        reordered.AddRange(interfaces);
        reordered.AddRange(implementations);
        reordered.AddRange(tests);

        _logger.LogInformation(
            "Enforced Abstractions First: {DtoCount} DTOs, {InterfaceCount} Interfaces, {ImplCount} Implementations",
            dtos.Count, interfaces.Count, implementations.Count);

        return reordered;
    }

    /// <summary>
    /// Asks the LLM to refactor the plan to break circular dependencies.
    /// Currently implements a simple heuristic by removing the last dependency.
    /// TODO: Implement actual LLM-based refactoring.
    /// </summary>
    private async Task<List<Atom>> RefactorToBreakCycleAsync(List<Atom> atoms, string errorMessage)
    {
        _logger.LogInformation("Requesting LLM to refactor and break circular dependency");

        // For now, just remove one dependency to break the cycle
        // In a real implementation, you'd call OpenAI to refactor
        
        var atomWithMostDeps = atoms.OrderByDescending(a => a.Dependencies.Count).First();
        if (atomWithMostDeps.Dependencies.Any())
        {
            _logger.LogWarning("Breaking cycle by removing dependency from {AtomId}", atomWithMostDeps.Id);
            atomWithMostDeps.Dependencies.RemoveAt(atomWithMostDeps.Dependencies.Count - 1);
        }

        return atoms;
    }
}
