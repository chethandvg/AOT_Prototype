using AoTEngine.AtomicAgent.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AoTEngine.AtomicAgent.Blackboard;

/// <summary>
/// Implements the Blackboard Pattern - a shared knowledge base for the solution.
/// Manages the solution_manifest.json file (Section 4 of the architectural blueprint).
/// </summary>
public class BlackboardService
{
    private readonly string _manifestPath;
    private readonly bool _autoSave;
    private readonly ILogger<BlackboardService> _logger;
    private SolutionManifest _manifest = null!;
    private readonly object _lock = new();

    public BlackboardService(
        string workspaceRoot, 
        string manifestFileName, 
        bool autoSave,
        ILogger<BlackboardService> logger)
    {
        _manifestPath = Path.Combine(workspaceRoot, manifestFileName);
        _autoSave = autoSave;
        _logger = logger;
        
        // Load or create manifest
        if (File.Exists(_manifestPath))
        {
            LoadManifest();
        }
        else
        {
            _manifest = new SolutionManifest();
            InitializeDefaultLayers();
            SaveManifest();
        }
    }

    public SolutionManifest Manifest
    {
        get
        {
            lock (_lock)
            {
                return _manifest;
            }
        }
    }

    /// <summary>
    /// Initializes the default Clean Architecture layers.
    /// </summary>
    private void InitializeDefaultLayers()
    {
        _manifest.ProjectHierarchy.Layers["Core"] = new Layer
        {
            Description = "Domain entities, Interfaces, and DTOs. Zero external dependencies.",
            ProjectPath = "src/Core/Core.csproj",
            AllowedDependencies = new List<string>()
        };

        _manifest.ProjectHierarchy.Layers["Infrastructure"] = new Layer
        {
            Description = "Implementation of Core interfaces. Database access, File I/O, External APIs.",
            ProjectPath = "src/Infrastructure/Infrastructure.csproj",
            AllowedDependencies = new List<string> { "Core" }
        };

        _manifest.ProjectHierarchy.Layers["Presentation"] = new Layer
        {
            Description = "Console UI or API endpoints. Entry point of the application.",
            ProjectPath = "src/Presentation/Presentation.csproj",
            AllowedDependencies = new List<string> { "Core", "Infrastructure" }
        };
    }

    /// <summary>
    /// Loads the manifest from disk.
    /// </summary>
    public void LoadManifest()
    {
        lock (_lock)
        {
            try
            {
                var json = File.ReadAllText(_manifestPath);
                _manifest = JsonConvert.DeserializeObject<SolutionManifest>(json) 
                    ?? new SolutionManifest();
                _logger.LogInformation("Loaded manifest from {Path}", _manifestPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load manifest, creating new one");
                _manifest = new SolutionManifest();
                InitializeDefaultLayers();
            }
        }
    }

    /// <summary>
    /// Saves the manifest to disk.
    /// </summary>
    public void SaveManifest()
    {
        lock (_lock)
        {
            try
            {
                _manifest.ProjectMetadata.LastUpdated = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(_manifest, Formatting.Indented);
                File.WriteAllText(_manifestPath, json);
                _logger.LogDebug("Saved manifest to {Path}", _manifestPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save manifest");
            }
        }
    }

    /// <summary>
    /// Adds or updates an atom in the manifest.
    /// </summary>
    public void UpsertAtom(Atom atom)
    {
        lock (_lock)
        {
            var existing = _manifest.Atoms.FirstOrDefault(a => a.Id == atom.Id);
            if (existing != null)
            {
                _manifest.Atoms.Remove(existing);
            }
            _manifest.Atoms.Add(atom);

            if (_autoSave)
            {
                SaveManifest();
            }
        }
    }

    /// <summary>
    /// Gets an atom by ID.
    /// </summary>
    public Atom? GetAtom(string atomId)
    {
        lock (_lock)
        {
            return _manifest.Atoms.FirstOrDefault(a => a.Id == atomId);
        }
    }

    /// <summary>
    /// Updates atom status.
    /// </summary>
    public void UpdateAtomStatus(string atomId, string status)
    {
        lock (_lock)
        {
            var atom = _manifest.Atoms.FirstOrDefault(a => a.Id == atomId);
            if (atom != null)
            {
                atom.Status = status;
                if (_autoSave)
                {
                    SaveManifest();
                }
            }
        }
    }

    /// <summary>
    /// Adds an interface signature to the Semantic Symbol Table.
    /// </summary>
    public void AddInterfaceSignature(InterfaceSignature signature)
    {
        lock (_lock)
        {
            // Remove existing if present
            _manifest.SemanticSymbolTable.Interfaces.RemoveAll(i => 
                i.Name == signature.Name && i.Namespace == signature.Namespace);
            
            _manifest.SemanticSymbolTable.Interfaces.Add(signature);

            if (_autoSave)
            {
                SaveManifest();
            }
        }
    }

    /// <summary>
    /// Adds a DTO signature to the Semantic Symbol Table.
    /// </summary>
    public void AddDtoSignature(DtoSignature signature)
    {
        lock (_lock)
        {
            // Remove existing if present
            _manifest.SemanticSymbolTable.Dtos.RemoveAll(d => 
                d.Name == signature.Name && d.Namespace == signature.Namespace);
            
            _manifest.SemanticSymbolTable.Dtos.Add(signature);

            if (_autoSave)
            {
                SaveManifest();
            }
        }
    }

    /// <summary>
    /// Gets all atoms with a specific status.
    /// </summary>
    public List<Atom> GetAtomsByStatus(string status)
    {
        lock (_lock)
        {
            return _manifest.Atoms.Where(a => a.Status == status).ToList();
        }
    }

    /// <summary>
    /// Checks if all dependencies of an atom are completed.
    /// </summary>
    public bool AreDependenciesSatisfied(Atom atom)
    {
        lock (_lock)
        {
            foreach (var depId in atom.Dependencies)
            {
                var dep = _manifest.Atoms.FirstOrDefault(a => a.Id == depId);
                if (dep == null || dep.Status != AtomStatus.Completed)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Validates architectural constraints (e.g., Core should not depend on Infrastructure).
    /// </summary>
    public bool ValidateLayerDependencies(Atom atom)
    {
        lock (_lock)
        {
            if (!_manifest.ProjectHierarchy.Layers.TryGetValue(atom.Layer, out var layer))
            {
                _logger.LogWarning("Unknown layer: {Layer}", atom.Layer);
                return true; // Allow if layer not defined
            }

            foreach (var depId in atom.Dependencies)
            {
                var dep = _manifest.Atoms.FirstOrDefault(a => a.Id == depId);
                if (dep != null && !layer.AllowedDependencies.Contains(dep.Layer))
                {
                    _logger.LogError(
                        "Architectural violation: {Layer} atom {AtomId} depends on {DepLayer} atom {DepId}",
                        atom.Layer, atom.Id, dep.Layer, dep.Id);
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Gets the semantic symbol table for context injection.
    /// </summary>
    public SemanticSymbolTable GetSemanticSymbolTable()
    {
        lock (_lock)
        {
            return _manifest.SemanticSymbolTable;
        }
    }
}
