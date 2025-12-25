namespace AoTEngine.Models;

/// <summary>
/// Represents a frozen API contract catalog containing all DTOs, Models, Enums, Interfaces, and Abstract classes.
/// This catalog is generated before implementations and serves as the single source of truth.
/// </summary>
public class ContractCatalog
{
    /// <summary>
    /// Project name for namespace prefixing.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Root namespace for the project.
    /// </summary>
    public string RootNamespace { get; set; } = string.Empty;

    /// <summary>
    /// All enum definitions in the project.
    /// </summary>
    public List<EnumContract> Enums { get; set; } = new();

    /// <summary>
    /// All interface definitions in the project.
    /// </summary>
    public List<InterfaceContract> Interfaces { get; set; } = new();

    /// <summary>
    /// All DTO/Model class definitions.
    /// </summary>
    public List<ModelContract> Models { get; set; } = new();

    /// <summary>
    /// All abstract class definitions.
    /// </summary>
    public List<AbstractClassContract> AbstractClasses { get; set; } = new();

    /// <summary>
    /// Whether the catalog has been frozen (no more modifications allowed).
    /// </summary>
    public bool IsFrozen { get; set; }

    /// <summary>
    /// Timestamp when the catalog was frozen.
    /// </summary>
    public DateTime? FrozenAtUtc { get; set; }

    /// <summary>
    /// Gets all contracts in the catalog.
    /// </summary>
    public IEnumerable<BaseContract> GetAllContracts()
    {
        foreach (var e in Enums) yield return e;
        foreach (var i in Interfaces) yield return i;
        foreach (var m in Models) yield return m;
        foreach (var a in AbstractClasses) yield return a;
    }

    /// <summary>
    /// Checks if a type name exists in the catalog.
    /// </summary>
    public bool ContainsType(string typeName)
    {
        return GetAllContracts().Any(c => c.Name == typeName || c.FullyQualifiedName == typeName);
    }

    /// <summary>
    /// Gets a contract by name or fully qualified name.
    /// </summary>
    public BaseContract? GetContract(string typeName)
    {
        return GetAllContracts().FirstOrDefault(c => c.Name == typeName || c.FullyQualifiedName == typeName);
    }

    /// <summary>
    /// Freezes the catalog, preventing further modifications.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        FrozenAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Base class for all contract definitions.
/// </summary>
public abstract class BaseContract
{
    /// <summary>
    /// Simple type name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Namespace for the type.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified name (Namespace.Name).
    /// </summary>
    public string FullyQualifiedName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";

    /// <summary>
    /// XML documentation comment for the type.
    /// </summary>
    public string Documentation { get; set; } = string.Empty;

    /// <summary>
    /// Access modifier (public, internal, etc.).
    /// </summary>
    public string AccessModifier { get; set; } = "public";

    /// <summary>
    /// The task ID that generated this contract (for tracking).
    /// </summary>
    public string SourceTaskId { get; set; } = string.Empty;

    /// <summary>
    /// Target file path for this contract.
    /// </summary>
    public string TargetFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Generates the C# code for this contract.
    /// </summary>
    public abstract string GenerateCode();
}

/// <summary>
/// Represents an enum contract definition.
/// </summary>
public class EnumContract : BaseContract
{
    /// <summary>
    /// Enum members with their values.
    /// </summary>
    public List<EnumMemberContract> Members { get; set; } = new();

    /// <summary>
    /// Whether this enum has the [Flags] attribute.
    /// </summary>
    public bool IsFlags { get; set; }

    /// <summary>
    /// Underlying type (default is int).
    /// </summary>
    public string UnderlyingType { get; set; } = "int";

    public override string GenerateCode()
    {
        var sb = new System.Text.StringBuilder();
        
        if (!string.IsNullOrEmpty(Documentation))
        {
            sb.AppendLine($"/// <summary>");
            sb.AppendLine($"/// {Documentation}");
            sb.AppendLine($"/// </summary>");
        }
        
        if (IsFlags)
        {
            sb.AppendLine("[Flags]");
        }
        
        var typeSpec = UnderlyingType != "int" ? $" : {UnderlyingType}" : "";
        sb.AppendLine($"{AccessModifier} enum {Name}{typeSpec}");
        sb.AppendLine("{");
        
        for (int i = 0; i < Members.Count; i++)
        {
            var member = Members[i];
            var comma = i < Members.Count - 1 ? "," : "";
            
            if (!string.IsNullOrEmpty(member.Documentation))
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {member.Documentation}");
                sb.AppendLine($"    /// </summary>");
            }
            
            if (member.Value.HasValue)
            {
                sb.AppendLine($"    {member.Name} = {member.Value}{comma}");
            }
            else
            {
                sb.AppendLine($"    {member.Name}{comma}");
            }
        }
        
        sb.AppendLine("}");
        return sb.ToString();
    }
}

/// <summary>
/// Represents an enum member.
/// </summary>
public class EnumMemberContract
{
    /// <summary>
    /// Member name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Explicit value (optional).
    /// </summary>
    public int? Value { get; set; }

    /// <summary>
    /// Documentation comment.
    /// </summary>
    public string Documentation { get; set; } = string.Empty;
}

/// <summary>
/// Represents an interface contract definition.
/// </summary>
public class InterfaceContract : BaseContract
{
    /// <summary>
    /// Base interfaces this interface extends.
    /// </summary>
    public List<string> BaseInterfaces { get; set; } = new();

    /// <summary>
    /// Method signatures defined by this interface.
    /// </summary>
    public List<MethodSignatureContract> Methods { get; set; } = new();

    /// <summary>
    /// Property signatures defined by this interface.
    /// </summary>
    public List<PropertySignatureContract> Properties { get; set; } = new();

    /// <summary>
    /// Generic type parameters.
    /// </summary>
    public List<string> TypeParameters { get; set; } = new();

    /// <summary>
    /// Generic type constraints.
    /// </summary>
    public Dictionary<string, List<string>> TypeConstraints { get; set; } = new();

    public override string GenerateCode()
    {
        var sb = new System.Text.StringBuilder();
        
        if (!string.IsNullOrEmpty(Documentation))
        {
            sb.AppendLine($"/// <summary>");
            sb.AppendLine($"/// {Documentation}");
            sb.AppendLine($"/// </summary>");
        }
        
        var typeParams = TypeParameters.Any() ? $"<{string.Join(", ", TypeParameters)}>" : "";
        var baseList = BaseInterfaces.Any() ? $" : {string.Join(", ", BaseInterfaces)}" : "";
        
        sb.AppendLine($"{AccessModifier} interface {Name}{typeParams}{baseList}");
        
        // Add type constraints
        foreach (var (typeParam, constraints) in TypeConstraints)
        {
            sb.AppendLine($"    where {typeParam} : {string.Join(", ", constraints)}");
        }
        
        sb.AppendLine("{");
        
        foreach (var prop in Properties)
        {
            if (!string.IsNullOrEmpty(prop.Documentation))
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {prop.Documentation}");
                sb.AppendLine($"    /// </summary>");
            }
            
            var accessors = new List<string>();
            if (prop.HasGetter) accessors.Add("get;");
            if (prop.HasSetter) accessors.Add("set;");
            
            sb.AppendLine($"    {prop.Type} {prop.Name} {{ {string.Join(" ", accessors)} }}");
        }
        
        foreach (var method in Methods)
        {
            if (!string.IsNullOrEmpty(method.Documentation))
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {method.Documentation}");
                sb.AppendLine($"    /// </summary>");
            }
            
            var methodTypeParams = method.TypeParameters.Any() 
                ? $"<{string.Join(", ", method.TypeParameters)}>" 
                : "";
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            
            sb.AppendLine($"    {method.ReturnType} {method.Name}{methodTypeParams}({parameters});");
        }
        
        sb.AppendLine("}");
        return sb.ToString();
    }
}

/// <summary>
/// Represents a method signature.
/// </summary>
public class MethodSignatureContract
{
    /// <summary>
    /// Method name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Return type.
    /// </summary>
    public string ReturnType { get; set; } = "void";

    /// <summary>
    /// Method parameters.
    /// </summary>
    public List<ParameterContract> Parameters { get; set; } = new();

    /// <summary>
    /// Generic type parameters.
    /// </summary>
    public List<string> TypeParameters { get; set; } = new();

    /// <summary>
    /// Documentation comment.
    /// </summary>
    public string Documentation { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is an async method.
    /// </summary>
    public bool IsAsync { get; set; }
}

/// <summary>
/// Represents a method parameter.
/// </summary>
public class ParameterContract
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Default value (optional).
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Whether this is a params array.
    /// </summary>
    public bool IsParams { get; set; }

    /// <summary>
    /// Whether this is a ref parameter.
    /// </summary>
    public bool IsRef { get; set; }

    /// <summary>
    /// Whether this is an out parameter.
    /// </summary>
    public bool IsOut { get; set; }
}

/// <summary>
/// Represents a property signature.
/// </summary>
public class PropertySignatureContract
{
    /// <summary>
    /// Property name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Property type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the property has a getter.
    /// </summary>
    public bool HasGetter { get; set; } = true;

    /// <summary>
    /// Whether the property has a setter.
    /// </summary>
    public bool HasSetter { get; set; }

    /// <summary>
    /// Documentation comment.
    /// </summary>
    public string Documentation { get; set; } = string.Empty;
}

/// <summary>
/// Represents a DTO/Model class contract.
/// </summary>
public class ModelContract : BaseContract
{
    /// <summary>
    /// Properties of the model.
    /// </summary>
    public List<PropertySignatureContract> Properties { get; set; } = new();

    /// <summary>
    /// Whether this is a record type.
    /// </summary>
    public bool IsRecord { get; set; }

    /// <summary>
    /// Base class (if any).
    /// </summary>
    public string? BaseClass { get; set; }

    /// <summary>
    /// Interfaces implemented by this model.
    /// </summary>
    public List<string> ImplementedInterfaces { get; set; } = new();

    public override string GenerateCode()
    {
        var sb = new System.Text.StringBuilder();
        
        if (!string.IsNullOrEmpty(Documentation))
        {
            sb.AppendLine($"/// <summary>");
            sb.AppendLine($"/// {Documentation}");
            sb.AppendLine($"/// </summary>");
        }
        
        var keyword = IsRecord ? "record" : "class";
        var baseList = new List<string>();
        if (!string.IsNullOrEmpty(BaseClass)) baseList.Add(BaseClass);
        baseList.AddRange(ImplementedInterfaces);
        var inheritance = baseList.Any() ? $" : {string.Join(", ", baseList)}" : "";
        
        sb.AppendLine($"{AccessModifier} {keyword} {Name}{inheritance}");
        sb.AppendLine("{");
        
        foreach (var prop in Properties)
        {
            if (!string.IsNullOrEmpty(prop.Documentation))
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {prop.Documentation}");
                sb.AppendLine($"    /// </summary>");
            }
            
            var accessors = new List<string>();
            if (prop.HasGetter) accessors.Add("get;");
            if (prop.HasSetter) accessors.Add("set;");
            
            sb.AppendLine($"    public {prop.Type} {prop.Name} {{ {string.Join(" ", accessors)} }}");
        }
        
        sb.AppendLine("}");
        return sb.ToString();
    }
}

/// <summary>
/// Represents an abstract class contract.
/// </summary>
public class AbstractClassContract : BaseContract
{
    /// <summary>
    /// Whether this class is sealed.
    /// </summary>
    public bool IsSealed { get; set; }

    /// <summary>
    /// Base class (if any).
    /// </summary>
    public string? BaseClass { get; set; }

    /// <summary>
    /// Interfaces implemented by this class.
    /// </summary>
    public List<string> ImplementedInterfaces { get; set; } = new();

    /// <summary>
    /// Abstract methods that must be implemented.
    /// </summary>
    public List<MethodSignatureContract> AbstractMethods { get; set; } = new();

    /// <summary>
    /// Virtual methods with default implementations.
    /// </summary>
    public List<MethodSignatureContract> VirtualMethods { get; set; } = new();

    /// <summary>
    /// Properties of this abstract class.
    /// </summary>
    public List<PropertySignatureContract> Properties { get; set; } = new();

    /// <summary>
    /// Generic type parameters.
    /// </summary>
    public List<string> TypeParameters { get; set; } = new();

    public override string GenerateCode()
    {
        var sb = new System.Text.StringBuilder();
        
        if (!string.IsNullOrEmpty(Documentation))
        {
            sb.AppendLine($"/// <summary>");
            sb.AppendLine($"/// {Documentation}");
            sb.AppendLine($"/// </summary>");
        }
        
        var sealedKeyword = IsSealed ? "sealed " : "";
        var typeParams = TypeParameters.Any() ? $"<{string.Join(", ", TypeParameters)}>" : "";
        
        var baseList = new List<string>();
        if (!string.IsNullOrEmpty(BaseClass)) baseList.Add(BaseClass);
        baseList.AddRange(ImplementedInterfaces);
        var inheritance = baseList.Any() ? $" : {string.Join(", ", baseList)}" : "";
        
        sb.AppendLine($"{AccessModifier} {sealedKeyword}abstract class {Name}{typeParams}{inheritance}");
        sb.AppendLine("{");
        
        foreach (var prop in Properties)
        {
            if (!string.IsNullOrEmpty(prop.Documentation))
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {prop.Documentation}");
                sb.AppendLine($"    /// </summary>");
            }
            
            var accessors = new List<string>();
            if (prop.HasGetter) accessors.Add("get;");
            if (prop.HasSetter) accessors.Add("set;");
            
            sb.AppendLine($"    public {prop.Type} {prop.Name} {{ {string.Join(" ", accessors)} }}");
        }
        
        foreach (var method in AbstractMethods)
        {
            if (!string.IsNullOrEmpty(method.Documentation))
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {method.Documentation}");
                sb.AppendLine($"    /// </summary>");
            }
            
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            sb.AppendLine($"    public abstract {method.ReturnType} {method.Name}({parameters});");
        }
        
        foreach (var method in VirtualMethods)
        {
            if (!string.IsNullOrEmpty(method.Documentation))
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {method.Documentation}");
                sb.AppendLine($"    /// </summary>");
            }
            
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            sb.AppendLine($"    public virtual {method.ReturnType} {method.Name}({parameters})");
            sb.AppendLine("    {");
            sb.AppendLine("        throw new NotImplementedException();");
            sb.AppendLine("    }");
        }
        
        sb.AppendLine("}");
        return sb.ToString();
    }
}
