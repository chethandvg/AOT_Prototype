using AoTEngine.AtomicAgent.Blackboard;
using AoTEngine.AtomicAgent.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AoTEngine.AtomicAgent.Context;

/// <summary>
/// Implements tiered context injection strategy (Section 7 of the blueprint).
/// Tier 1: Global Context (The "Map")
/// Tier 2: Local Context (The "Tools") 
/// Tier 3: Target Context (The "Task")
/// </summary>
public class ContextEngine
{
    private readonly BlackboardService _blackboard;
    private readonly IMemoryCache _hotCache;
    private readonly ILogger<ContextEngine> _logger;
    private readonly bool _enableHotCache;

    public ContextEngine(
        BlackboardService blackboard,
        IMemoryCache hotCache,
        ILogger<ContextEngine> logger,
        bool enableHotCache = true)
    {
        _blackboard = blackboard;
        _hotCache = hotCache;
        _logger = logger;
        _enableHotCache = enableHotCache;
    }

    /// <summary>
    /// Builds the complete context for code generation using tiered injection.
    /// </summary>
    public string BuildContext(Atom targetAtom)
    {
        var contextParts = new List<string>();

        // Tier 1: Global Context
        contextParts.Add(BuildGlobalContext());

        // Tier 2: Local Context
        contextParts.Add(BuildLocalContext(targetAtom));

        // Tier 3: Target Context
        contextParts.Add(BuildTargetContext(targetAtom));

        return string.Join("\n\n", contextParts);
    }

    /// <summary>
    /// Tier 1: Global Context - Project structure, naming conventions, completed files.
    /// </summary>
    private string BuildGlobalContext()
    {
        var manifest = _blackboard.Manifest;
        var context = $@"=== GLOBAL CONTEXT (The Map) ===

Project Name: {manifest.ProjectMetadata.Name}
Root Namespace: {manifest.ProjectMetadata.RootNamespace}
Target Framework: {manifest.ProjectMetadata.TargetFramework}

Architecture Layers:
{string.Join("\n", manifest.ProjectHierarchy.Layers.Select(kvp => 
    $"  - {kvp.Key}: {kvp.Value.Description}\n    Dependencies: {string.Join(", ", kvp.Value.AllowedDependencies)}"))}

Completed Files:
{string.Join("\n", _blackboard.GetAtomsByStatus(AtomStatus.Completed).Select(a => $"  - {a.FilePath}"))}

IMPORTANT ARCHITECTURAL RULES:
1. Core layer has ZERO external dependencies
2. Infrastructure depends ONLY on Core
3. Presentation depends on Core and Infrastructure
4. Always define interfaces in Core before implementations in Infrastructure
";

        return context;
    }

    /// <summary>
    /// Tier 2: Local Context - Semantic signatures of direct dependencies.
    /// </summary>
    private string BuildLocalContext(Atom targetAtom)
    {
        var signatures = new List<string>();
        var sst = _blackboard.GetSemanticSymbolTable();

        foreach (var depId in targetAtom.Dependencies)
        {
            var dep = _blackboard.GetAtom(depId);
            if (dep == null) continue;

            // Try to get from hot cache first
            string? signature = null;
            if (_enableHotCache)
            {
                signature = _hotCache.Get<string>($"signature_{dep.Id}");
            }

            if (signature == null)
            {
                // Get from Semantic Symbol Table
                if (dep.Type == AtomType.Interface)
                {
                    var iface = sst.Interfaces.FirstOrDefault(i => i.Name == dep.Name);
                    if (iface != null)
                    {
                        signature = FormatInterfaceSignature(iface);
                    }
                }
                else if (dep.Type == AtomType.Dto)
                {
                    var dto = sst.Dtos.FirstOrDefault(d => d.Name == dep.Name);
                    if (dto != null)
                    {
                        signature = FormatDtoSignature(dto);
                    }
                }

                // Cache it
                if (signature != null && _enableHotCache)
                {
                    _hotCache.Set($"signature_{dep.Id}", signature, 
                        new MemoryCacheEntryOptions
                        {
                            SlidingExpiration = TimeSpan.FromMinutes(30)
                        });
                }
            }

            if (signature != null)
            {
                signatures.Add(signature);
            }
        }

        var context = $@"=== LOCAL CONTEXT (The Tools) ===

Available Dependencies (signatures only, NOT full implementations):
{string.Join("\n\n", signatures)}

CRITICAL: Use ONLY these exact signatures. DO NOT modify method names or parameters.
";

        return signatures.Any() ? context : "";
    }

    /// <summary>
    /// Tier 3: Target Context - Specific requirements for this atom.
    /// </summary>
    private string BuildTargetContext(Atom targetAtom)
    {
        var context = $@"=== TARGET CONTEXT (The Task) ===

Atom ID: {targetAtom.Id}
Type: {targetAtom.Type}
Name: {targetAtom.Name}
Layer: {targetAtom.Layer}
File Path: {targetAtom.FilePath}

YOUR TASK:
Generate the complete C# code for {targetAtom.Name}.

Type-Specific Instructions:
{GetTypeSpecificInstructions(targetAtom.Type)}

REQUIREMENTS:
1. Use namespace: {_blackboard.Manifest.ProjectMetadata.RootNamespace}.{targetAtom.Layer}
2. Follow C# naming conventions (PascalCase for public members)
3. Include necessary using statements
4. Add XML documentation comments
5. Output ONLY valid C# code wrapped in ```csharp ... ``` markers
";

        return context;
    }

    /// <summary>
    /// Gets type-specific code generation instructions.
    /// </summary>
    private string GetTypeSpecificInstructions(string atomType)
    {
        return atomType switch
        {
            AtomType.Dto => 
                "- Create a simple class or record with properties\n" +
                "- NO methods or logic (DTOs are data-only)\n" +
                "- Use init or required properties for immutability",
            
            AtomType.Interface =>
                "- Define an interface with method signatures\n" +
                "- NO implementation code\n" +
                "- Use descriptive method names following I{Name} convention",
            
            AtomType.Implementation =>
                "- Implement ALL methods from the interface(s)\n" +
                "- Use constructor injection for dependencies\n" +
                "- Include error handling and validation",
            
            AtomType.Test =>
                "- Use xUnit framework\n" +
                "- Include [Fact] or [Theory] attributes\n" +
                "- Test all public methods",
            
            _ => "- Follow C# best practices"
        };
    }

    /// <summary>
    /// Formats an interface signature for context injection.
    /// </summary>
    private string FormatInterfaceSignature(InterfaceSignature iface)
    {
        return $@"// Interface: {iface.Namespace}.{iface.Name}
public interface {iface.Name}
{{
{string.Join("\n", iface.Methods.Select(m => $"    {m};"))}
}}";
    }

    /// <summary>
    /// Formats a DTO signature for context injection.
    /// </summary>
    private string FormatDtoSignature(DtoSignature dto)
    {
        return $@"// DTO: {dto.Namespace}.{dto.Name}
public class {dto.Name}
{{
{string.Join("\n", dto.Properties.Select(p => $"    {p}"))}
}}";
    }

    /// <summary>
    /// Caches generated code in hot cache for quick retrieval.
    /// </summary>
    public void CacheCode(string atomId, string code)
    {
        if (_enableHotCache)
        {
            _hotCache.Set($"code_{atomId}", code,
                new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });
            _logger.LogDebug("Cached code for atom {AtomId}", atomId);
        }
    }

    /// <summary>
    /// Retrieves code from hot cache.
    /// </summary>
    public string? GetCachedCode(string atomId)
    {
        if (_enableHotCache)
        {
            return _hotCache.Get<string>($"code_{atomId}");
        }
        return null;
    }
}
