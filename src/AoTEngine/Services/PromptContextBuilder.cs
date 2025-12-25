using AoTEngine.Models;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Builds enhanced prompt context with contract definitions, known symbols, and guardrails.
/// This prevents the model from redefining types or using incorrect signatures.
/// </summary>
public class PromptContextBuilder
{
    private readonly ContractCatalog? _contractCatalog;
    private readonly SymbolTable _symbolTable;
    private readonly TypeRegistry _typeRegistry;

    public PromptContextBuilder(
        ContractCatalog? contractCatalog,
        SymbolTable symbolTable,
        TypeRegistry typeRegistry)
    {
        _contractCatalog = contractCatalog;
        _symbolTable = symbolTable;
        _typeRegistry = typeRegistry;
    }

    /// <summary>
    /// Builds complete context for code generation including contracts, known types, and guardrails.
    /// </summary>
    /// <param name="task">The task to generate code for.</param>
    /// <param name="completedTasks">Already completed tasks.</param>
    /// <returns>Enhanced context string for prompt injection.</returns>
    public string BuildCodeGenerationContext(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        var sb = new StringBuilder();

        // Add strict guardrails
        sb.AppendLine(GenerateGuardrails());
        sb.AppendLine();

        // Add frozen contracts if available
        if (_contractCatalog != null && _contractCatalog.IsFrozen)
        {
            sb.AppendLine(GenerateContractBlock(task));
            sb.AppendLine();
        }

        // Add known types block
        var knownTypesBlock = _symbolTable.GenerateKnownTypesBlock();
        if (!string.IsNullOrEmpty(knownTypesBlock))
        {
            sb.AppendLine(knownTypesBlock);
            sb.AppendLine();
        }

        // Add ambiguity warnings
        var ambiguityWarnings = GenerateAmbiguityWarnings(task);
        if (!string.IsNullOrEmpty(ambiguityWarnings))
        {
            sb.AppendLine(ambiguityWarnings);
            sb.AppendLine();
        }

        // Add task-specific context
        sb.AppendLine($"/* TASK REQUIREMENTS */");
        sb.AppendLine($"Task ID: {task.Id}");
        sb.AppendLine($"Description: {task.Description}");
        
        if (!string.IsNullOrEmpty(task.Namespace))
        {
            sb.AppendLine($"Target Namespace: {task.Namespace}");
        }

        if (task.ExpectedTypes.Any())
        {
            sb.AppendLine($"Expected Types to Generate: {string.Join(", ", task.ExpectedTypes)}");
        }

        if (!string.IsNullOrEmpty(task.Context))
        {
            sb.AppendLine($"Additional Context: {task.Context}");
        }

        // Add dependency context
        if (task.Dependencies.Any())
        {
            sb.AppendLine();
            sb.AppendLine("/* DEPENDENCY CONTRACTS (use these types exactly) */");
            
            foreach (var depId in task.Dependencies)
            {
                if (completedTasks.TryGetValue(depId, out var depTask))
                {
                    sb.AppendLine($"// From {depId}:");
                    if (!string.IsNullOrEmpty(depTask.TypeContract))
                    {
                        sb.AppendLine(depTask.TypeContract);
                    }
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates strict guardrails for the model.
    /// </summary>
    private string GenerateGuardrails()
    {
        return @"/* CRITICAL RULES - VIOLATIONS WILL CAUSE COMPILATION ERRORS */

1. DO NOT redefine any types listed in 'KNOWN TYPES' or 'FROZEN CONTRACTS'
2. DO NOT create duplicate type names, even in different namespaces (causes ambiguity)
3. IMPLEMENT all interface members with EXACT return types and parameter signatures
4. IMPLEMENT all abstract methods from base classes with EXACT signatures
5. USE only enum members that exist in the contract (do not invent new ones)
6. DO NOT inherit from sealed classes - use composition instead
7. PLACE DTOs/Models in the '.Models' namespace, NOT in '.Services'
8. USE fully qualified type names when there could be ambiguity
9. MATCH async/await patterns exactly (Task<T> vs T, CancellationToken, etc.)
10. ADD all required using statements at the top of the file

FAILURE TO FOLLOW THESE RULES WILL RESULT IN COMPILATION ERRORS.";
    }

    /// <summary>
    /// Generates the contract block for task-relevant contracts.
    /// </summary>
    private string GenerateContractBlock(TaskNode task)
    {
        if (_contractCatalog == null || !_contractCatalog.IsFrozen)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("/* FROZEN CONTRACTS - These are the ONLY valid type definitions */");
        sb.AppendLine();

        // Find contracts relevant to this task
        var relevantEnums = GetRelevantEnums(task);
        var relevantInterfaces = GetRelevantInterfaces(task);
        var relevantAbstractClasses = GetRelevantAbstractClasses(task);
        var relevantModels = GetRelevantModels(task);

        // Add enum contracts
        if (relevantEnums.Any())
        {
            sb.AppendLine("// ENUMS - Use these exact member names:");
            foreach (var e in relevantEnums)
            {
                sb.AppendLine($"// {e.FullyQualifiedName}");
                sb.AppendLine($"// enum {e.Name} {{ {string.Join(", ", e.Members.Select(m => m.Name))} }}");
            }
            sb.AppendLine();
        }

        // Add interface contracts with full signatures
        if (relevantInterfaces.Any())
        {
            sb.AppendLine("// INTERFACES - Implement ALL methods with EXACT signatures:");
            foreach (var i in relevantInterfaces)
            {
                sb.AppendLine($"// {i.FullyQualifiedName}");
                sb.AppendLine(i.GenerateCode());
            }
            sb.AppendLine();
        }

        // Add abstract class contracts
        if (relevantAbstractClasses.Any())
        {
            sb.AppendLine("// ABSTRACT CLASSES - Override ALL abstract methods:");
            foreach (var a in relevantAbstractClasses)
            {
                var sealedNote = a.IsSealed ? " [SEALED - DO NOT INHERIT, USE COMPOSITION]" : "";
                sb.AppendLine($"// {a.FullyQualifiedName}{sealedNote}");
                sb.AppendLine(a.GenerateCode());
            }
            sb.AppendLine();
        }

        // Add model contracts
        if (relevantModels.Any())
        {
            sb.AppendLine("// MODELS - Use these exact property types:");
            foreach (var m in relevantModels)
            {
                sb.AppendLine($"// {m.FullyQualifiedName}");
                foreach (var prop in m.Properties)
                {
                    sb.AppendLine($"//   {prop.Type} {prop.Name}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets enums relevant to a task.
    /// </summary>
    private List<EnumContract> GetRelevantEnums(TaskNode task)
    {
        if (_contractCatalog == null) return new List<EnumContract>();

        // Get enums in the task's namespace or consumed from dependencies
        return _contractCatalog.Enums
            .Where(e => IsRelevantToTask(e, task))
            .ToList();
    }

    /// <summary>
    /// Gets interfaces relevant to a task.
    /// </summary>
    private List<InterfaceContract> GetRelevantInterfaces(TaskNode task)
    {
        if (_contractCatalog == null) return new List<InterfaceContract>();

        return _contractCatalog.Interfaces
            .Where(i => IsRelevantToTask(i, task))
            .ToList();
    }

    /// <summary>
    /// Gets abstract classes relevant to a task.
    /// </summary>
    private List<AbstractClassContract> GetRelevantAbstractClasses(TaskNode task)
    {
        if (_contractCatalog == null) return new List<AbstractClassContract>();

        return _contractCatalog.AbstractClasses
            .Where(a => IsRelevantToTask(a, task))
            .ToList();
    }

    /// <summary>
    /// Gets models relevant to a task.
    /// </summary>
    private List<ModelContract> GetRelevantModels(TaskNode task)
    {
        if (_contractCatalog == null) return new List<ModelContract>();

        return _contractCatalog.Models
            .Where(m => IsRelevantToTask(m, task))
            .ToList();
    }

    /// <summary>
    /// Checks if a contract is relevant to a task.
    /// </summary>
    private bool IsRelevantToTask(BaseContract contract, TaskNode task)
    {
        // Contract is in the same namespace
        if (!string.IsNullOrEmpty(task.Namespace) && 
            (contract.Namespace == task.Namespace || 
             contract.Namespace.StartsWith(task.Namespace) ||
             task.Namespace.StartsWith(contract.Namespace)))
        {
            return true;
        }

        // Contract is referenced in expected types
        if (task.ExpectedTypes.Contains(contract.Name))
        {
            return true;
        }

        // Contract is consumed from dependencies
        if (task.ConsumedTypes != null)
        {
            foreach (var consumedList in task.ConsumedTypes.Values)
            {
                if (consumedList.Contains(contract.Name))
                {
                    return true;
                }
            }
        }

        // Check if contract type is mentioned in task description
        if (task.Description.Contains(contract.Name))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generates warnings about potential type ambiguities.
    /// </summary>
    private string GenerateAmbiguityWarnings(TaskNode task)
    {
        var warnings = new List<string>();

        // Check for ambiguous type names in the registry
        if (task.ExpectedTypes.Any())
        {
            foreach (var typeName in task.ExpectedTypes)
            {
                if (_typeRegistry.IsAmbiguous(typeName))
                {
                    var matches = _typeRegistry.GetTypesBySimpleName(typeName);
                    warnings.Add($"WARNING: '{typeName}' exists in multiple namespaces: {string.Join(", ", matches.Select(m => m.FullyQualifiedName))}");
                    warnings.Add($"  → Use fully qualified name to avoid ambiguity");
                }
            }
        }

        if (!warnings.Any())
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("/* AMBIGUITY WARNINGS */");
        foreach (var warning in warnings)
        {
            sb.AppendLine(warning);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Builds context for interface implementation with exact signatures.
    /// </summary>
    public string BuildInterfaceImplementationContext(string interfaceName, TaskNode task)
    {
        if (_contractCatalog == null)
        {
            return string.Empty;
        }

        var interfaceContract = _contractCatalog.Interfaces.FirstOrDefault(i => 
            i.Name == interfaceName || i.FullyQualifiedName == interfaceName);

        if (interfaceContract == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"/* INTERFACE IMPLEMENTATION REQUIREMENTS for {interfaceName} */");
        sb.AppendLine($"You MUST implement ALL of the following methods with EXACT signatures:");
        sb.AppendLine();

        foreach (var method in interfaceContract.Methods)
        {
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            sb.AppendLine($"  {method.ReturnType} {method.Name}({parameters})");
        }

        foreach (var prop in interfaceContract.Properties)
        {
            var accessors = new List<string>();
            if (prop.HasGetter) accessors.Add("get");
            if (prop.HasSetter) accessors.Add("set");
            sb.AppendLine($"  {prop.Type} {prop.Name} {{ {string.Join("; ", accessors)}; }}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds context for abstract class implementation with exact signatures.
    /// </summary>
    public string BuildAbstractClassImplementationContext(string abstractClassName, TaskNode task)
    {
        if (_contractCatalog == null)
        {
            return string.Empty;
        }

        var abstractContract = _contractCatalog.AbstractClasses.FirstOrDefault(a => 
            a.Name == abstractClassName || a.FullyQualifiedName == abstractClassName);

        if (abstractContract == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        
        if (abstractContract.IsSealed)
        {
            sb.AppendLine($"/* WARNING: {abstractClassName} is SEALED */");
            sb.AppendLine("DO NOT inherit from this class. Use composition instead:");
            sb.AppendLine($"  private readonly {abstractClassName} _inner;");
            return sb.ToString();
        }

        sb.AppendLine($"/* ABSTRACT CLASS IMPLEMENTATION REQUIREMENTS for {abstractClassName} */");
        sb.AppendLine($"You MUST override ALL of the following abstract methods:");
        sb.AppendLine();

        foreach (var method in abstractContract.AbstractMethods)
        {
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            sb.AppendLine($"  public override {method.ReturnType} {method.Name}({parameters})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds context for enum usage with valid members.
    /// </summary>
    public string BuildEnumUsageContext(string enumName)
    {
        if (_contractCatalog == null)
        {
            return string.Empty;
        }

        var enumContract = _contractCatalog.Enums.FirstOrDefault(e => 
            e.Name == enumName || e.FullyQualifiedName == enumName);

        if (enumContract == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"/* VALID ENUM MEMBERS for {enumName} */");
        sb.AppendLine($"Only these values are valid (do not use any other values):");
        
        foreach (var member in enumContract.Members)
        {
            sb.AppendLine($"  - {enumName}.{member.Name}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Validates that generated code doesn't redefine frozen contracts.
    /// </summary>
    public List<string> ValidateAgainstContracts(string generatedCode, TaskNode task)
    {
        var errors = new List<string>();

        if (_contractCatalog == null || !_contractCatalog.IsFrozen)
        {
            return errors;
        }

        // Check for redefined types
        foreach (var contract in _contractCatalog.GetAllContracts())
        {
            // Simple check - look for type definition patterns
            var patterns = new[]
            {
                $"class {contract.Name}",
                $"interface {contract.Name}",
                $"enum {contract.Name}",
                $"record {contract.Name}",
                $"struct {contract.Name}"
            };

            foreach (var pattern in patterns)
            {
                if (generatedCode.Contains(pattern) && 
                    !task.ExpectedTypes.Contains(contract.Name))
                {
                    errors.Add($"ERROR: Code redefines frozen contract type '{contract.Name}'. Use the existing definition from {contract.FullyQualifiedName}.");
                }
            }
        }

        return errors;
    }
}
