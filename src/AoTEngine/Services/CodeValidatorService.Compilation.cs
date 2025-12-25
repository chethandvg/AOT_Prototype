using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing compilation and assembly resolution methods.
/// </summary>
public partial class CodeValidatorService
{
    /// <summary>
    /// Attempts compilation and automatically resolves missing assemblies, retrying up to MaxAssemblyRetries times.
    /// </summary>
    private ValidationResult AttemptCompilationWithAssemblyResolution(
        string code, 
        SyntaxTree syntaxTree, 
        List<MetadataReference> references)
    {
        var result = new ValidationResult { IsValid = true };
        var addedAssemblies = new HashSet<string>();

        for (int attempt = 0; attempt < MaxAssemblyRetries; attempt++)
        {
            var assemblyName = $"GeneratedAssembly_{Guid.NewGuid():N}";
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            EmitResult emitResult = compilation.Emit(ms);

            if (emitResult.Success)
            {
                result.IsValid = true;
                result.Errors.Clear();
                Console.WriteLine("✓ Code validated successfully!");
                return result;
            }

            // Compilation failed - analyze errors
            result.IsValid = false;
            result.Errors.Clear();
            result.Warnings.Clear();

            var missingNamespaces = new HashSet<string>();
            var missingAssemblies = new HashSet<string>();

            foreach (var diagnostic in emitResult.Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    var errorMessage = diagnostic.GetMessage();
                    result.Errors.Add($"Compilation Error: {errorMessage}");

                    // Extract missing namespaces and assemblies
                    ExtractMissingReferences(errorMessage, missingNamespaces, missingAssemblies);
                }
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                {
                    result.Warnings.Add($"Compilation Warning: {diagnostic.GetMessage()}");
                }
            }

            // If this is the last attempt, return the errors
            if (attempt == MaxAssemblyRetries - 1)
            {
                Console.WriteLine($"✗ Compilation failed with {result.Errors.Count} error(s) after {MaxAssemblyRetries} attempts");
                return result;
            }

            // Try to resolve missing references
            bool addedNewReferences = TryAddMissingReferences(
                code, 
                missingNamespaces, 
                missingAssemblies, 
                references, 
                addedAssemblies);

            if (!addedNewReferences)
            {
                // No new references could be added, return current errors
                Console.WriteLine($"✗ Compilation failed - no additional assemblies could be resolved");
                return result;
            }

            Console.WriteLine($"🔄 Retrying compilation with additional assemblies (attempt {attempt + 2}/{MaxAssemblyRetries})...");
        }

        return result;
    }

    /// <summary>
    /// Extracts missing namespace and assembly information from compiler error messages.
    /// </summary>
    private void ExtractMissingReferences(
        string errorMessage, 
        HashSet<string> missingNamespaces, 
        HashSet<string> missingAssemblies)
    {
        // Pattern 1: "The type or namespace name 'X' does not exist in the namespace 'Y'"
        var namespacePattern = @"The type or namespace name '([^']+)' does not exist in the namespace '([^']+)'";
        var namespaceMatch = Regex.Match(errorMessage, namespacePattern);
        if (namespaceMatch.Success)
        {
            var parentNamespace = namespaceMatch.Groups[2].Value;
            var childName = namespaceMatch.Groups[1].Value;
            missingNamespaces.Add($"{parentNamespace}.{childName}");
            Console.WriteLine($"   🔍 Detected missing namespace: {parentNamespace}.{childName}");
        }

        // Pattern 2: "The type or namespace name 'X' could not be found"
        var typePattern = @"The type or namespace name '([^'<>]+)<?[^']*>?' could not be found";
        var typeMatch = Regex.Match(errorMessage, typePattern);
        if (typeMatch.Success)
        {
            var typeName = typeMatch.Groups[1].Value;
            missingNamespaces.Add(typeName);
            Console.WriteLine($"   🔍 Detected missing type: {typeName}");
        }

        // Pattern 3: "You must add a reference to assembly 'X'"
        var assemblyPattern = @"You must add a reference to assembly '([^']+)'";
        var assemblyMatch = Regex.Match(errorMessage, assemblyPattern);
        if (assemblyMatch.Success)
        {
            var assemblyName = assemblyMatch.Groups[1].Value;
            // Extract just the assembly name without version/culture/token
            var simpleAssemblyName = assemblyName.Split(',')[0];
            missingAssemblies.Add(simpleAssemblyName);
            Console.WriteLine($"   🔍 Detected missing assembly: {simpleAssemblyName}");
        }

        // Pattern 4: "'X' does not contain a definition for 'Y'" - extension method issue
        var extensionPattern = @"'([^']+)' does not contain a definition for '([^']+)'.*extension method";
        var extensionMatch = Regex.Match(errorMessage, extensionPattern);
        if (extensionMatch.Success)
        {
            var methodName = extensionMatch.Groups[2].Value;
            // Common extension method patterns
            var commonExtensionMappings = new Dictionary<string, string>
            {
                { "SetBasePath", "Microsoft.Extensions.Configuration.FileExtensions" },
                { "AddJsonFile", "Microsoft.Extensions.Configuration.Json" },
                { "AddUserSecrets", "Microsoft.Extensions.Configuration.UserSecrets" },
                { "AddEnvironmentVariables", "Microsoft.Extensions.Configuration.EnvironmentVariables" }
            };

            if (commonExtensionMappings.TryGetValue(methodName, out var assemblyForExtension))
            {
                missingAssemblies.Add(assemblyForExtension);
                Console.WriteLine($"   🔍 Detected extension method '{methodName}' requires: {assemblyForExtension}");
            }
        }

        // Pattern 5: "The name 'X' does not exist in the current context"
        var contextPattern = @"The name '([^']+)' does not exist in the current context";
        var contextMatch = Regex.Match(errorMessage, contextPattern);
        if (contextMatch.Success)
        {
            var name = contextMatch.Groups[1].Value;
            // Common type to namespace mappings
            var commonTypeMappings = new Dictionary<string, string>
            {
                { "Host", "Microsoft.Extensions.Hosting" },
                { "IHost", "Microsoft.Extensions.Hosting" },
                { "IHostBuilder", "Microsoft.Extensions.Hosting" },
                { "IConfiguration", "Microsoft.Extensions.Configuration" },
                { "ILogger", "Microsoft.Extensions.Logging" },
                { "IServiceCollection", "Microsoft.Extensions.DependencyInjection" }
            };

            if (commonTypeMappings.TryGetValue(name, out var namespaceForType))
            {
                missingNamespaces.Add(namespaceForType);
                Console.WriteLine($"   🔍 Detected type '{name}' requires namespace: {namespaceForType}");
            }
        }
    }

    /// <summary>
    /// Attempts to add missing references from the assembly mappings.
    /// </summary>
    private bool TryAddMissingReferences(
        string code,
        HashSet<string> missingNamespaces,
        HashSet<string> missingAssemblies,
        List<MetadataReference> references,
        HashSet<string> alreadyAdded)
    {
        bool addedAny = false;
        var assemblyConfig = LoadAssemblyConfig();

        // Try to add assemblies for missing namespaces
        foreach (var ns in missingNamespaces)
        {
            if (TryResolveNamespaceToAssemblies(ns, assemblyConfig, references, alreadyAdded))
            {
                addedAny = true;
            }
        }

        // Try to add directly specified assemblies
        foreach (var assembly in missingAssemblies)
        {
            if (!alreadyAdded.Contains(assembly))
            {
                if (TryLoadAssembly(assembly, references))
                {
                    alreadyAdded.Add(assembly);
                    addedAny = true;
                    Console.WriteLine($"   ✓ Added assembly: {assembly}");
                }
            }
        }

        return addedAny;
    }

    /// <summary>
    /// Resolves a namespace to one or more assemblies and adds them.
    /// </summary>
    private bool TryResolveNamespaceToAssemblies(
        string ns,
        AssemblyMappingConfig config,
        List<MetadataReference> references,
        HashSet<string> alreadyAdded)
    {
        bool added = false;

        // Direct mapping
        if (config.NamespaceToAssemblyMappings.TryGetValue(ns, out var assemblyName))
        {
            if (!alreadyAdded.Contains(assemblyName))
            {
                if (TryLoadAssembly(assemblyName, references))
                {
                    alreadyAdded.Add(assemblyName);
                    added = true;
                    Console.WriteLine($"   ✓ Added assembly '{assemblyName}' for namespace '{ns}'");
                }
            }
        }

        // Check for partial matches (e.g., Microsoft.Extensions.DependencyInjection.Abstractions)
        var relatedMappings = config.NamespaceToAssemblyMappings
            .Where(kvp => ns.StartsWith(kvp.Key) || kvp.Key.StartsWith(ns))
            .ToList();

        foreach (var mapping in relatedMappings)
        {
            if (!alreadyAdded.Contains(mapping.Value))
            {
                if (TryLoadAssembly(mapping.Value, references))
                {
                    alreadyAdded.Add(mapping.Value);
                    added = true;
                    Console.WriteLine($"   ✓ Added related assembly '{mapping.Value}' for namespace '{ns}'");
                }
            }
        }

        return added;
    }

    /// <summary>
    /// Attempts to load an assembly and add it to references.
    /// </summary>
    private bool TryLoadAssembly(string assemblyName, List<MetadataReference> references)
    {
        try
        {
            var assembly = System.Reflection.Assembly.Load(assemblyName);
            var reference = MetadataReference.CreateFromFile(assembly.Location);
            
            if (!references.Any(r => r.Display == reference.Display))
            {
                references.Add(reference);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
