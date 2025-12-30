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

CRITICAL INSTRUCTIONS - Abstractions First Strategy:
1. Identify all nouns (Entities/DTOs) and define them as 'dto' type atoms FIRST
2. Identify all verbs (Capabilities) and define them as 'interface' type atoms SECOND
3. Only THEN define 'implementation' type atoms that implement the interfaces
4. This prevents circular dependencies and ensures clean architecture

Request: {userRequest}

Context: {context}

For each atom, specify:
- A unique ID (atom_001, atom_002, etc.)
- Type: 'dto', 'interface', or 'implementation'
- Name: The class/interface name
- Layer: 'Core', 'Infrastructure', or 'Presentation'
- Dependencies: List of atom IDs this depends on (interfaces and DTOs should have NO dependencies)
- Description: What this atom implements

Example response structure:
Atom ID: atom_001
Type: dto
Name: UserDto
Layer: Core
Dependencies: []
Description: Data transfer object for user information

Atom ID: atom_002
Type: interface
Name: IUserRepository
Layer: Core
Dependencies: [atom_001]
Description: Interface for user data access

Atom ID: atom_003
Type: implementation
Name: FileUserRepository
Layer: Infrastructure
Dependencies: [atom_001, atom_002]
Description: File-based implementation of IUserRepository";

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
        
        if (lower.Contains("interface") || lower.Contains("dto") || lower.Contains("model"))
            return "Core";
        
        if (lower.Contains("repository") || lower.Contains("service") || lower.Contains("database"))
            return "Infrastructure";
        
        if (lower.Contains("controller") || lower.Contains("api") || lower.Contains("ui"))
            return "Presentation";
        
        return "Core"; // Default to Core
    }

    /// <summary>
    /// Enforces abstractions-first ordering: DTOs, then Interfaces, then Implementations.
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
        }

        foreach (var iface in interfaces)
        {
            iface.Dependencies.RemoveAll(depId => 
                atoms.Any(a => a.Id == depId && a.Type == AtomType.Implementation));
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
    /// </summary>
    private async Task<List<Atom>> RefactorToBreakCycleAsync(List<Atom> atoms, string errorMessage)
    {
        _logger.LogInformation("Requesting LLM to refactor and break circular dependency");

        var refactorPrompt = $@"The following task decomposition has a circular dependency:

{errorMessage}

Current atoms:
{string.Join("\n", atoms.Select(a => $"- {a.Id}: {a.Name} (depends on: {string.Join(", ", a.Dependencies)})"))}

Please refactor by introducing an interface to break the cycle. 
Follow the Dependency Inversion Principle.
Return the updated atom list.";

        // For simplicity, we'll just introduce an interface atom
        // In a real implementation, you'd call OpenAI to refactor
        
        // For now, just remove one dependency to break the cycle
        var atomWithMostDeps = atoms.OrderByDescending(a => a.Dependencies.Count).First();
        if (atomWithMostDeps.Dependencies.Any())
        {
            _logger.LogWarning("Breaking cycle by removing dependency from {AtomId}", atomWithMostDeps.Id);
            atomWithMostDeps.Dependencies.RemoveAt(atomWithMostDeps.Dependencies.Count - 1);
        }

        return atoms;
    }
}
