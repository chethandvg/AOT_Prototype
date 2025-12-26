using AoTEngine.Models;
using Newtonsoft.Json;

namespace AoTEngine.Services;

/// <summary>
/// Service for managing contract manifests - saving, loading, and validating contract catalogs.
/// </summary>
public class ContractManifestService
{
    private const string ManifestFileName = "contracts.json";

    /// <summary>
    /// Saves a contract catalog to a JSON manifest file.
    /// </summary>
    /// <param name="catalog">The contract catalog to save.</param>
    /// <param name="outputDirectory">Directory to save the manifest to.</param>
    /// <returns>Path to the saved manifest file.</returns>
    public async Task<string> SaveManifestAsync(ContractCatalog catalog, string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
        var json = JsonConvert.SerializeObject(catalog, new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto
        });

        await File.WriteAllTextAsync(manifestPath, json);
        return manifestPath;
    }

    /// <summary>
    /// Loads a contract catalog from a JSON manifest file.
    /// </summary>
    /// <param name="outputDirectory">Directory containing the manifest.</param>
    /// <returns>The loaded contract catalog, or null if not found.</returns>
    public async Task<ContractCatalog?> LoadManifestAsync(string outputDirectory)
    {
        var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
        
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(manifestPath);
        return JsonConvert.DeserializeObject<ContractCatalog>(json, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto
        });
    }

    /// <summary>
    /// Validates that a type name exists in the frozen contract catalog.
    /// </summary>
    /// <param name="catalog">The contract catalog.</param>
    /// <param name="typeName">Type name to validate.</param>
    /// <returns>True if the type exists in the catalog.</returns>
    public bool ValidateTypeExists(ContractCatalog catalog, string typeName)
    {
        return catalog.ContainsType(typeName);
    }

    /// <summary>
    /// Validates that an enum member exists in the catalog.
    /// </summary>
    /// <param name="catalog">The contract catalog.</param>
    /// <param name="enumName">Enum type name.</param>
    /// <param name="memberName">Member name to validate.</param>
    /// <returns>True if the member exists in the enum.</returns>
    public bool ValidateEnumMember(ContractCatalog catalog, string enumName, string memberName)
    {
        var enumContract = catalog.Enums.FirstOrDefault(e => 
            e.Name == enumName || e.FullyQualifiedName == enumName);
        
        if (enumContract == null)
        {
            return false;
        }

        return enumContract.Members.Any(m => m.Name == memberName);
    }

    /// <summary>
    /// Validates that an interface method signature matches the catalog.
    /// </summary>
    /// <param name="catalog">The contract catalog.</param>
    /// <param name="interfaceName">Interface name.</param>
    /// <param name="methodName">Method name.</param>
    /// <param name="returnType">Expected return type.</param>
    /// <param name="parameterTypes">Expected parameter types.</param>
    /// <returns>Validation result with details.</returns>
    public ContractValidationResult ValidateInterfaceMethod(
        ContractCatalog catalog,
        string interfaceName,
        string methodName,
        string returnType,
        List<string> parameterTypes)
    {
        var result = new ContractValidationResult();
        
        var interfaceContract = catalog.Interfaces.FirstOrDefault(i => 
            i.Name == interfaceName || i.FullyQualifiedName == interfaceName);
        
        if (interfaceContract == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Interface '{interfaceName}' not found in contract catalog");
            return result;
        }

        var method = interfaceContract.Methods.FirstOrDefault(m => m.Name == methodName);
        
        if (method == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Method '{methodName}' not found in interface '{interfaceName}'");
            return result;
        }

        // Validate return type
        if (method.ReturnType != returnType)
        {
            result.IsValid = false;
            result.Errors.Add($"Return type mismatch for '{interfaceName}.{methodName}': expected '{method.ReturnType}', got '{returnType}'");
        }

        // Validate parameter types
        var methodParamTypes = method.Parameters.Select(p => p.Type).ToList();
        if (!methodParamTypes.SequenceEqual(parameterTypes))
        {
            result.IsValid = false;
            result.Errors.Add($"Parameter type mismatch for '{interfaceName}.{methodName}': expected ({string.Join(", ", methodParamTypes)}), got ({string.Join(", ", parameterTypes)})");
        }

        return result;
    }

    /// <summary>
    /// Validates that an abstract method is properly implemented.
    /// </summary>
    /// <param name="catalog">The contract catalog.</param>
    /// <param name="abstractClassName">Abstract class name.</param>
    /// <param name="methodName">Method name.</param>
    /// <param name="returnType">Implementation return type.</param>
    /// <param name="parameterTypes">Implementation parameter types.</param>
    /// <returns>Validation result with details.</returns>
    public ContractValidationResult ValidateAbstractMethodImplementation(
        ContractCatalog catalog,
        string abstractClassName,
        string methodName,
        string returnType,
        List<string> parameterTypes)
    {
        var result = new ContractValidationResult();
        
        var abstractClass = catalog.AbstractClasses.FirstOrDefault(a => 
            a.Name == abstractClassName || a.FullyQualifiedName == abstractClassName);
        
        if (abstractClass == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Abstract class '{abstractClassName}' not found in contract catalog");
            return result;
        }

        var method = abstractClass.AbstractMethods.FirstOrDefault(m => m.Name == methodName);
        
        if (method == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Abstract method '{methodName}' not found in class '{abstractClassName}'");
            return result;
        }

        // Validate return type
        if (method.ReturnType != returnType)
        {
            result.IsValid = false;
            result.Errors.Add($"Return type mismatch for '{abstractClassName}.{methodName}': expected '{method.ReturnType}', got '{returnType}'");
        }

        // Validate parameter types
        var methodParamTypes = method.Parameters.Select(p => p.Type).ToList();
        if (!methodParamTypes.SequenceEqual(parameterTypes))
        {
            result.IsValid = false;
            result.Errors.Add($"Parameter type mismatch for '{abstractClassName}.{methodName}': expected ({string.Join(", ", methodParamTypes)}), got ({string.Join(", ", parameterTypes)})");
        }

        return result;
    }

    /// <summary>
    /// Checks if a type is sealed in the catalog.
    /// </summary>
    /// <param name="catalog">The contract catalog.</param>
    /// <param name="typeName">Type name to check.</param>
    /// <returns>True if the type is sealed.</returns>
    public bool IsTypeSealed(ContractCatalog catalog, string typeName)
    {
        var abstractClass = catalog.AbstractClasses.FirstOrDefault(a => 
            a.Name == typeName || a.FullyQualifiedName == typeName);
        
        return abstractClass?.IsSealed ?? false;
    }

    /// <summary>
    /// Gets all enum member names for a specific enum.
    /// </summary>
    /// <param name="catalog">The contract catalog.</param>
    /// <param name="enumName">Enum name.</param>
    /// <returns>List of member names, or empty list if enum not found.</returns>
    public List<string> GetEnumMembers(ContractCatalog catalog, string enumName)
    {
        var enumContract = catalog.Enums.FirstOrDefault(e => 
            e.Name == enumName || e.FullyQualifiedName == enumName);
        
        return enumContract?.Members.Select(m => m.Name).ToList() ?? new List<string>();
    }

    /// <summary>
    /// Gets all interface methods for a specific interface.
    /// </summary>
    /// <param name="catalog">The contract catalog.</param>
    /// <param name="interfaceName">Interface name.</param>
    /// <returns>List of method signatures.</returns>
    public List<MethodSignatureContract> GetInterfaceMethods(ContractCatalog catalog, string interfaceName)
    {
        var interfaceContract = catalog.Interfaces.FirstOrDefault(i => 
            i.Name == interfaceName || i.FullyQualifiedName == interfaceName);
        
        return interfaceContract?.Methods ?? new List<MethodSignatureContract>();
    }

    /// <summary>
    /// Gets all abstract methods for a specific abstract class.
    /// </summary>
    /// <param name="catalog">The contract catalog.</param>
    /// <param name="abstractClassName">Abstract class name.</param>
    /// <returns>List of abstract method signatures.</returns>
    public List<MethodSignatureContract> GetAbstractMethods(ContractCatalog catalog, string abstractClassName)
    {
        var abstractClass = catalog.AbstractClasses.FirstOrDefault(a => 
            a.Name == abstractClassName || a.FullyQualifiedName == abstractClassName);
        
        return abstractClass?.AbstractMethods ?? new List<MethodSignatureContract>();
    }

    /// <summary>
    /// Generates code files for all contracts in the catalog.
    /// </summary>
    /// <param name="catalog">The contract catalog.</param>
    /// <param name="outputDirectory">Output directory for generated files.</param>
    /// <returns>List of generated file paths.</returns>
    public async Task<List<string>> GenerateContractFilesAsync(ContractCatalog catalog, string outputDirectory)
    {
        var generatedFiles = new List<string>();

        foreach (var contract in catalog.GetAllContracts())
        {
            var code = GenerateContractFileCode(contract, catalog);
            var filePath = GetContractFilePath(contract, outputDirectory);
            
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, code);
            generatedFiles.Add(filePath);
            contract.TargetFilePath = filePath;
        }

        return generatedFiles;
    }

    private string GenerateContractFileCode(BaseContract contract, ContractCatalog catalog)
    {
        var sb = new System.Text.StringBuilder();
        
        // Add standard usings
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        
        // Add namespace
        sb.AppendLine($"namespace {contract.Namespace};");
        sb.AppendLine();
        
        // Add the contract code
        sb.Append(contract.GenerateCode());
        
        return sb.ToString();
    }

    private string GetContractFilePath(BaseContract contract, string outputDirectory)
    {
        // Convert namespace to directory structure
        var relativePath = contract.Namespace.Replace('.', Path.DirectorySeparatorChar);
        var fileName = $"{contract.Name}.cs";
        
        return Path.Combine(outputDirectory, relativePath, fileName);
    }
}

/// <summary>
/// Result of contract validation.
/// </summary>
public class ContractValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
