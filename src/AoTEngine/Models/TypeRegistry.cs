using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AoTEngine.Models;

/// <summary>
/// Registry entry for a type discovered during code synthesis.
/// </summary>
public class TypeRegistryEntry
{
    /// <summary>
    /// Fully qualified type name (Namespace.TypeName).
    /// </summary>
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>
    /// The namespace the type belongs to.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// The simple type name.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// The kind of type (class, interface, record, enum, struct).
    /// </summary>
    public ProjectTypeKind Kind { get; set; }

    /// <summary>
    /// The task ID that owns/defined this type.
    /// </summary>
    public string OwnerTaskId { get; set; } = string.Empty;

    /// <summary>
    /// Target output file path for this type.
    /// </summary>
    public string TargetFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether this type is declared as partial.
    /// </summary>
    public bool IsPartial { get; set; }

    /// <summary>
    /// List of member signatures (constructors, methods, properties).
    /// </summary>
    public List<MemberSignature> Members { get; set; } = new();

    /// <summary>
    /// The original syntax node for this type declaration.
    /// </summary>
    public TypeDeclarationSyntax? SyntaxNode { get; set; }
}

/// <summary>
/// Represents a member signature for conflict detection.
/// </summary>
public class MemberSignature
{
    /// <summary>
    /// Member name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of member (constructor, method, property, field).
    /// </summary>
    public ProjectMemberKind Kind { get; set; }

    /// <summary>
    /// Parameter types for methods/constructors.
    /// </summary>
    public List<string> ParameterTypes { get; set; } = new();

    /// <summary>
    /// Return type for methods/properties.
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// Unique signature string for comparison.
    /// </summary>
    public string SignatureKey => Kind == ProjectMemberKind.Constructor || Kind == ProjectMemberKind.Method
        ? $"{Name}({string.Join(",", ParameterTypes)})"
        : Name;
}

/// <summary>
/// Kinds of types tracked in the registry.
/// </summary>
public enum ProjectTypeKind
{
    Class,
    Interface,
    Record,
    Enum,
    Struct
}

/// <summary>
/// Kinds of members tracked in type entries.
/// </summary>
public enum ProjectMemberKind
{
    Constructor,
    Method,
    Property,
    Field,
    Event
}

/// <summary>
/// Conflict information when duplicate types are detected.
/// </summary>
public class TypeConflict
{
    /// <summary>
    /// The fully qualified type name that has conflicts.
    /// </summary>
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>
    /// The existing type entry.
    /// </summary>
    public TypeRegistryEntry ExistingEntry { get; set; } = null!;

    /// <summary>
    /// The new conflicting type entry.
    /// </summary>
    public TypeRegistryEntry NewEntry { get; set; } = null!;

    /// <summary>
    /// Type of conflict detected.
    /// </summary>
    public ConflictType ConflictType { get; set; }

    /// <summary>
    /// Conflicting member signatures, if applicable.
    /// </summary>
    public List<MemberSignature> ConflictingMembers { get; set; } = new();

    /// <summary>
    /// Suggested resolution for this conflict.
    /// </summary>
    public ConflictResolution SuggestedResolution { get; set; }
}

/// <summary>
/// Types of conflicts that can be detected.
/// </summary>
public enum ConflictType
{
    /// <summary>
    /// Same type defined in multiple tasks.
    /// </summary>
    DuplicateType,

    /// <summary>
    /// Same member defined multiple times in a type.
    /// </summary>
    DuplicateMember,

    /// <summary>
    /// Same type name in different namespaces (ambiguity).
    /// </summary>
    AmbiguousTypeName
}

/// <summary>
/// Possible conflict resolutions.
/// </summary>
public enum ConflictResolution
{
    /// <summary>
    /// Keep the first definition, reject later ones.
    /// </summary>
    KeepFirst,

    /// <summary>
    /// Merge as partial class (only valid for classes).
    /// </summary>
    MergeAsPartial,

    /// <summary>
    /// Remove the duplicate from the later task.
    /// </summary>
    RemoveDuplicate,

    /// <summary>
    /// Fail and require manual intervention.
    /// </summary>
    FailFast,

    /// <summary>
    /// Fully qualify type references to resolve ambiguity.
    /// </summary>
    UseFullyQualifiedName
}

/// <summary>
/// Registry for tracking all types across tasks to detect conflicts and enable deduplication.
/// </summary>
public class TypeRegistry
{
    private readonly Dictionary<string, TypeRegistryEntry> _typesByFullName = new();
    private readonly Dictionary<string, List<TypeRegistryEntry>> _typesBySimpleName = new();
    private readonly List<TypeConflict> _conflicts = new();

    /// <summary>
    /// All registered types.
    /// </summary>
    public IReadOnlyDictionary<string, TypeRegistryEntry> Types => _typesByFullName;

    /// <summary>
    /// All detected conflicts.
    /// </summary>
    public IReadOnlyList<TypeConflict> Conflicts => _conflicts;

    /// <summary>
    /// Registers a type and detects any conflicts.
    /// </summary>
    /// <param name="entry">The type entry to register.</param>
    /// <returns>True if registered without conflict, false if conflict detected.</returns>
    public bool TryRegister(TypeRegistryEntry entry)
    {
        var key = entry.FullyQualifiedName;

        // Check for duplicate type
        if (_typesByFullName.TryGetValue(key, out var existing))
        {
            var conflict = CreateConflict(existing, entry);
            _conflicts.Add(conflict);
            return false;
        }

        _typesByFullName[key] = entry;

        // Track by simple name for ambiguity detection
        if (!_typesBySimpleName.TryGetValue(entry.TypeName, out var simpleNameList))
        {
            simpleNameList = new List<TypeRegistryEntry>();
            _typesBySimpleName[entry.TypeName] = simpleNameList;
        }
        simpleNameList.Add(entry);

        return true;
    }

    /// <summary>
    /// Checks if a type is already registered.
    /// </summary>
    public bool Contains(string fullyQualifiedName) => _typesByFullName.ContainsKey(fullyQualifiedName);

    /// <summary>
    /// Gets a registered type by fully qualified name.
    /// </summary>
    public TypeRegistryEntry? GetType(string fullyQualifiedName) =>
        _typesByFullName.TryGetValue(fullyQualifiedName, out var entry) ? entry : null;

    /// <summary>
    /// Gets all types with the given simple name (for ambiguity detection).
    /// </summary>
    public IReadOnlyList<TypeRegistryEntry> GetTypesBySimpleName(string simpleName) =>
        _typesBySimpleName.TryGetValue(simpleName, out var list) ? list : Array.Empty<TypeRegistryEntry>();

    /// <summary>
    /// Checks if a simple type name is ambiguous (exists in multiple namespaces).
    /// </summary>
    public bool IsAmbiguous(string simpleName) =>
        _typesBySimpleName.TryGetValue(simpleName, out var list) && list.Count > 1;

    /// <summary>
    /// Gets types owned by a specific task.
    /// </summary>
    public IEnumerable<TypeRegistryEntry> GetTypesByOwner(string taskId) =>
        _typesByFullName.Values.Where(e => e.OwnerTaskId == taskId);

    /// <summary>
    /// Clears all registered types and conflicts.
    /// </summary>
    public void Clear()
    {
        _typesByFullName.Clear();
        _typesBySimpleName.Clear();
        _conflicts.Clear();
    }

    private TypeConflict CreateConflict(TypeRegistryEntry existing, TypeRegistryEntry newEntry)
    {
        var conflict = new TypeConflict
        {
            FullyQualifiedName = existing.FullyQualifiedName,
            ExistingEntry = existing,
            NewEntry = newEntry,
            ConflictType = ConflictType.DuplicateType
        };

        // Determine suggested resolution
        if (existing.Kind == ProjectTypeKind.Interface || existing.Kind == ProjectTypeKind.Enum)
        {
            // Interfaces and enums are single-owner: keep first or fail
            conflict.SuggestedResolution = ConflictResolution.KeepFirst;
        }
        else if (existing.Kind == ProjectTypeKind.Class && newEntry.Kind == ProjectTypeKind.Class)
        {
            // Check if they can be merged as partial
            var conflictingMembers = FindConflictingMembers(existing, newEntry);
            if (conflictingMembers.Count == 0)
            {
                conflict.SuggestedResolution = ConflictResolution.MergeAsPartial;
            }
            else
            {
                conflict.ConflictType = ConflictType.DuplicateMember;
                conflict.ConflictingMembers = conflictingMembers;
                conflict.SuggestedResolution = ConflictResolution.RemoveDuplicate;
            }
        }
        else
        {
            conflict.SuggestedResolution = ConflictResolution.FailFast;
        }

        return conflict;
    }

    private List<MemberSignature> FindConflictingMembers(TypeRegistryEntry existing, TypeRegistryEntry newEntry)
    {
        var existingSignatures = existing.Members.Select(m => m.SignatureKey).ToHashSet();
        return newEntry.Members.Where(m => existingSignatures.Contains(m.SignatureKey)).ToList();
    }
}
