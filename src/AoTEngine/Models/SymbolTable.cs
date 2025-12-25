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
/// </summary>
public class SymbolTable
{
    private readonly Dictionary<string, ProjectSymbolInfo> _symbols = new();
    private readonly Dictionary<string, HashSet<string>> _symbolsByNamespace = new();
    private readonly Dictionary<string, HashSet<string>> _symbolsByTask = new();

    /// <summary>
    /// All known symbols.
    /// </summary>
    public IReadOnlyDictionary<string, ProjectSymbolInfo> Symbols => _symbols;

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

        return true;
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
    }
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
