namespace AoTEngine.AtomicAgent.Models;

/// <summary>
/// Represents the solution manifest JSON schema as defined in the architectural blueprint.
/// This is the "Blackboard" - a shared knowledge base tracking structural, temporal, and semantic dimensions.
/// </summary>
public class SolutionManifest
{
    public ProjectMetadata ProjectMetadata { get; set; } = new();
    public ProjectHierarchy ProjectHierarchy { get; set; } = new();
    public SemanticSymbolTable SemanticSymbolTable { get; set; } = new();
    public List<Atom> Atoms { get; set; } = new();
}

public class ProjectMetadata
{
    public string Name { get; set; } = "AtomicAgentPrototype";
    public string RootNamespace { get; set; } = "AtomicAgent";
    public string TargetFramework { get; set; } = "net9.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class ProjectHierarchy
{
    public Dictionary<string, Layer> Layers { get; set; } = new();
}

public class Layer
{
    public string Description { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public List<string> AllowedDependencies { get; set; } = new();
}

public class SemanticSymbolTable
{
    public List<InterfaceSignature> Interfaces { get; set; } = new();
    public List<DtoSignature> Dtos { get; set; } = new();
}

public class InterfaceSignature
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<string> Methods { get; set; } = new();
}

public class DtoSignature
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<string> Properties { get; set; } = new();
}

/// <summary>
/// Represents an atomic unit of work in the dependency graph.
/// </summary>
public class Atom
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "implementation"; // interface, dto, implementation, test
    public string Name { get; set; } = string.Empty;
    public string Layer { get; set; } = "Core"; // Core, Infrastructure, Presentation
    public string Status { get; set; } = "pending"; // pending, in_progress, review, completed, failed
    public List<string> Dependencies { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public List<string> CompileErrors { get; set; } = new();
    public string GeneratedCode { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 0;
}

/// <summary>
/// Status values for atoms
/// </summary>
public static class AtomStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Review = "review";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

/// <summary>
/// Type values for atoms
/// </summary>
public static class AtomType
{
    public const string Interface = "interface";
    public const string Dto = "dto";
    public const string Implementation = "implementation";
    public const string Test = "test";
}
