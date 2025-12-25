namespace AoTEngine.Models;

/// <summary>
/// Symbol table entry for tracking known symbols across tasks.
/// </summary>
public class ProjectSymbolInfo
{
    /// <summary>
    /// Fully qualified symbol name.
    /// </summary>
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>
    /// The namespace this symbol belongs to.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Simple name of the symbol.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Kind of symbol (Type, Method, Property, etc.).
    /// </summary>
    public ProjectSymbolKind Kind { get; set; }

    /// <summary>
    /// The task that defined this symbol.
    /// </summary>
    public string DefinedByTaskId { get; set; } = string.Empty;

    /// <summary>
    /// Public signature (for interfaces, constructors, methods).
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Required using directives for this symbol.
    /// </summary>
    public List<string> RequiredUsings { get; set; } = new();

    /// <summary>
    /// Whether this is a public/accessible symbol.
    /// </summary>
    public bool IsPublic { get; set; } = true;
}

/// <summary>
/// Kinds of symbols tracked in the symbol table.
/// </summary>
public enum ProjectSymbolKind
{
    Type,
    Interface,
    Enum,
    Method,
    Property,
    Constructor
}

/// <summary>
/// Maintains a project-wide symbol table for tracking what types and members have been defined.
/// Used to prevent duplicate definitions and provide context to subsequent tasks.
/// Enhanced with collision detection and namespace enforcement.
/// </summary>
public class SymbolTable
{
    private readonly Dictionary<string, ProjectSymbolInfo> _symbols = new();
    private readonly Dictionary<string, HashSet<string>> _symbolsByNamespace = new();
    private readonly Dictionary<string, HashSet<string>> _symbolsByTask = new();
    private readonly Dictionary<string, HashSet<string>> _symbolsBySimpleName = new();
    private readonly List<SymbolCollision> _collisions = new();

    /// <summary>
    /// All known symbols.
    /// </summary>
    public IReadOnlyDictionary<string, ProjectSymbolInfo> Symbols => _symbols;

    /// <summary>
    /// All detected collisions.
    /// </summary>
    public IReadOnlyList<SymbolCollision> Collisions => _collisions;

    /// <summary>
    /// Registers a new symbol in the table.
    /// </summary>
    /// <param name="symbol">The symbol to register.</param>
    /// <returns>True if registered, false if already exists.</returns>
    public bool TryRegister(ProjectSymbolInfo symbol)
    {
        if (_symbols.ContainsKey(symbol.FullyQualifiedName))
        {
            return false;
        }

        _symbols[symbol.FullyQualifiedName] = symbol;

        // Index by namespace
        if (!_symbolsByNamespace.TryGetValue(symbol.Namespace, out var nsSymbols))
        {
            nsSymbols = new HashSet<string>();
            _symbolsByNamespace[symbol.Namespace] = nsSymbols;
        }
        nsSymbols.Add(symbol.FullyQualifiedName);

        // Index by task
        if (!string.IsNullOrEmpty(symbol.DefinedByTaskId))
        {
            if (!_symbolsByTask.TryGetValue(symbol.DefinedByTaskId, out var taskSymbols))
            {
                taskSymbols = new HashSet<string>();
                _symbolsByTask[symbol.DefinedByTaskId] = taskSymbols;
            }
            taskSymbols.Add(symbol.FullyQualifiedName);
        }

        // Index by simple name for collision detection
        if (!_symbolsBySimpleName.TryGetValue(symbol.Name, out var simpleNameSymbols))
        {
            simpleNameSymbols = new HashSet<string>();
            _symbolsBySimpleName[symbol.Name] = simpleNameSymbols;
        }
        else if (simpleNameSymbols.Any())
        {
            // Collision detected - same simple name in different namespaces
            _collisions.AddRange(simpleNameSymbols.Select(existingFqn => new SymbolCollision
            {
                SimpleName = symbol.Name,
                ExistingSymbol = _symbols[existingFqn],
                NewSymbol = symbol,
                CollisionType = DetermineCollisionType(_symbols[existingFqn], symbol)
            }));
        }
        simpleNameSymbols.Add(symbol.FullyQualifiedName);

        return true;
    }

    /// <summary>
    /// Determines the type of collision between two symbols.
    /// </summary>
    private SymbolCollisionType DetermineCollisionType(ProjectSymbolInfo existing, ProjectSymbolInfo newSymbol)
    {
        // Same namespace = duplicate definition
        if (existing.Namespace == newSymbol.Namespace)
        {
            return SymbolCollisionType.DuplicateDefinition;
        }

        // DTO in Services namespace when it should be in Models
        if (IsModelType(newSymbol) && newSymbol.Namespace.Contains(".Services"))
        {
            return SymbolCollisionType.MisplacedModel;
        }

        // Same simple name in different namespaces = ambiguity
        return SymbolCollisionType.AmbiguousName;
    }

    /// <summary>
    /// Checks if a symbol represents a model/DTO type.
    /// </summary>
    private bool IsModelType(ProjectSymbolInfo symbol)
    {
        // Heuristic: non-interface types with a model-like suffix.
        // We treat service-specific Request/Response types in Services namespaces
        // as non-models to avoid flagging legitimate service DTOs as misplaced.
        var name = symbol.Name;

        // Do not treat service-layer Request/Response types as models.
        if (symbol.Namespace.Contains(".Services") &&
            (name.EndsWith("Request") || name.EndsWith("Response")))
        {
            return false;
        }

        return symbol.Kind != ProjectSymbolKind.Interface &&
               (name.EndsWith("Info") ||
                name.EndsWith("Data") ||
                name.EndsWith("Dto") ||
                name.EndsWith("Model"));
    }

    /// <summary>
    /// Gets all symbols with a given simple name (for ambiguity detection).
    /// </summary>
    public IEnumerable<ProjectSymbolInfo> GetSymbolsBySimpleName(string simpleName)
    {
        if (_symbolsBySimpleName.TryGetValue(simpleName, out var fqns))
        {
            return fqns.Select(fqn => _symbols[fqn]);
        }
        return Enumerable.Empty<ProjectSymbolInfo>();
    }

    /// <summary>
    /// Checks if a simple name is ambiguous (exists in multiple namespaces).
    /// </summary>
    public bool IsAmbiguous(string simpleName)
    {
        return _symbolsBySimpleName.TryGetValue(simpleName, out var fqns) && fqns.Count > 1;
    }

    /// <summary>
    /// Validates namespace conventions for a symbol.
    /// Returns validation errors if conventions are violated.
    /// </summary>
    public List<string> ValidateNamespaceConventions(ProjectSymbolInfo symbol)
    {
        var errors = new List<string>();

        // Models/DTOs should be in .Models namespace
        if (IsModelType(symbol) && !symbol.Namespace.Contains(".Models") && symbol.Namespace.Contains(".Services"))
        {
            errors.Add($"Model type '{symbol.Name}' should be in a '.Models' namespace, not '{symbol.Namespace}'");
        }

        // Interfaces should have 'I' prefix
        if (symbol.Kind == ProjectSymbolKind.Interface && !symbol.Name.StartsWith("I"))
        {
            errors.Add($"Interface '{symbol.Name}' should have 'I' prefix");
        }

        return errors;
    }

    /// <summary>
    /// Gets a suggested alias for an ambiguous type.
    /// </summary>
    public string GetSuggestedAlias(string simpleName, string preferredNamespace)
    {
        var symbols = GetSymbolsBySimpleName(simpleName).ToList();
        
        if (symbols.Count <= 1)
        {
            return simpleName;
        }

        // Find the one in the preferred namespace
        var preferred = symbols.FirstOrDefault(s => s.Namespace == preferredNamespace);
        if (preferred != null)
        {
            return preferred.FullyQualifiedName;
        }

        // Return the first one with Models namespace (for DTOs)
        var modelsType = symbols.FirstOrDefault(s => s.Namespace.Contains(".Models"));
        if (modelsType != null)
        {
            return modelsType.FullyQualifiedName;
        }

        // Return first available
        return symbols.First().FullyQualifiedName;
    }

    /// <summary>
    /// Generates using alias suggestions for ambiguous types.
    /// </summary>
    public List<string> GenerateUsingAliases()
    {
        var aliases = new List<string>();

        foreach (var collision in _collisions.Where(c => c.CollisionType == SymbolCollisionType.AmbiguousName))
        {
            // Create alias for the new symbol
            var alias = $"{collision.NewSymbol.Namespace.Replace(".", "")}{collision.NewSymbol.Name}";
            aliases.Add($"using {alias} = {collision.NewSymbol.FullyQualifiedName};");
        }

        return aliases;
    }

    /// <summary>
    /// Gets a symbol by fully qualified name.
    /// </summary>
    public ProjectSymbolInfo? GetSymbol(string fullyQualifiedName) =>
        _symbols.TryGetValue(fullyQualifiedName, out var symbol) ? symbol : null;

    /// <summary>
    /// Checks if a symbol exists.
    /// </summary>
    public bool Contains(string fullyQualifiedName) => _symbols.ContainsKey(fullyQualifiedName);

    /// <summary>
    /// Gets all symbols in a namespace.
    /// </summary>
    public IEnumerable<ProjectSymbolInfo> GetSymbolsInNamespace(string ns)
    {
        if (_symbolsByNamespace.TryGetValue(ns, out var keys))
        {
            return keys.Select(k => _symbols[k]);
        }
        return Enumerable.Empty<ProjectSymbolInfo>();
    }

    /// <summary>
    /// Gets all symbols defined by a task.
    /// </summary>
    public IEnumerable<ProjectSymbolInfo> GetSymbolsByTask(string taskId)
    {
        if (_symbolsByTask.TryGetValue(taskId, out var keys))
        {
            return keys.Select(k => _symbols[k]);
        }
        return Enumerable.Empty<ProjectSymbolInfo>();
    }

    /// <summary>
    /// Generates a compact "Known Types" block for injection into prompts.
    /// </summary>
    public string GenerateKnownTypesBlock()
    {
        var types = _symbols.Values
            .Where(s => s.Kind == ProjectSymbolKind.Type || s.Kind == ProjectSymbolKind.Interface || s.Kind == ProjectSymbolKind.Enum)
            .OrderBy(s => s.FullyQualifiedName);

        if (!types.Any())
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("/* Existing types (DO NOT redefine - reference them fully qualified or via namespace):");
        
        foreach (var type in types)
        {
            var kindStr = type.Kind.ToString().ToLowerInvariant();
            sb.AppendLine($" * - {type.FullyQualifiedName} ({kindStr})");
            if (!string.IsNullOrEmpty(type.Signature))
            {
                sb.AppendLine($" *     {type.Signature}");
            }
        }
        
        sb.AppendLine(" */");
        return sb.ToString();
    }

    /// <summary>
    /// Generates metadata about defined types for structured output validation.
    /// </summary>
    public TypeDefinitionMetadata GenerateMetadata()
    {
        return new TypeDefinitionMetadata
        {
            DefinedTypes = _symbols.Values
                .Where(s => s.Kind == ProjectSymbolKind.Type || s.Kind == ProjectSymbolKind.Interface || s.Kind == ProjectSymbolKind.Enum)
                .Select(s => s.FullyQualifiedName)
                .ToList(),
            RequiredUsings = _symbols.Values
                .SelectMany(s => s.RequiredUsings)
                .Distinct()
                .ToList(),
            DependsOnTypes = new List<string>() // Populated during analysis
        };
    }

    /// <summary>
    /// Clears all symbols.
    /// </summary>
    public void Clear()
    {
        _symbols.Clear();
        _symbolsByNamespace.Clear();
        _symbolsByTask.Clear();
        _symbolsBySimpleName.Clear();
        _collisions.Clear();
    }
}

/// <summary>
/// Represents a collision between symbols.
/// </summary>
public class SymbolCollision
{
    /// <summary>
    /// The simple name that caused the collision.
    /// </summary>
    public string SimpleName { get; set; } = string.Empty;

    /// <summary>
    /// The existing symbol.
    /// </summary>
    public ProjectSymbolInfo ExistingSymbol { get; set; } = null!;

    /// <summary>
    /// The new symbol that collides.
    /// </summary>
    public ProjectSymbolInfo NewSymbol { get; set; } = null!;

    /// <summary>
    /// Type of collision.
    /// </summary>
    public SymbolCollisionType CollisionType { get; set; }
}

/// <summary>
/// Types of symbol collisions.
/// </summary>
public enum SymbolCollisionType
{
    /// <summary>Same type defined multiple times in same namespace.</summary>
    DuplicateDefinition,
    
    /// <summary>Same simple name in different namespaces (ambiguous).</summary>
    AmbiguousName,
    
    /// <summary>Model/DTO defined in wrong namespace (should be in .Models).</summary>
    MisplacedModel
}

/// <summary>
/// Metadata about types defined in generated code (for structured output validation).
/// </summary>
public class TypeDefinitionMetadata
{
    /// <summary>
    /// Types defined in the generated code.
    /// </summary>
    public List<string> DefinedTypes { get; set; } = new();

    /// <summary>
    /// Required using directives.
    /// </summary>
    public List<string> RequiredUsings { get; set; } = new();

    /// <summary>
    /// Types that this code depends on.
    /// </summary>
    public List<string> DependsOnTypes { get; set; } = new();
}
