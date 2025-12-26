using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing integration validation, dependency checking, and linting methods.
/// </summary>
public partial class CodeValidatorService
{
    /// <summary>
    /// Validates dependencies between tasks to ensure consumed types are available.
    /// </summary>
    public ValidationResult ValidateDependencies(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        var result = new ValidationResult { IsValid = true };
        
        if (task.ConsumedTypes == null || !task.ConsumedTypes.Any())
        {
            return result;
        }

        Console.WriteLine($"\n🔗 Validating dependencies for task '{task.Id}'...");
        
        foreach (var (depId, consumedTypes) in task.ConsumedTypes)
        {
            if (!completedTasks.TryGetValue(depId, out var depTask))
            {
                result.IsValid = false;
                result.Errors.Add($"Dependency task '{depId}' not found in completed tasks");
                continue;
            }

            if (string.IsNullOrWhiteSpace(depTask.GeneratedCode))
            {
                result.Warnings.Add($"Dependency task '{depId}' has no generated code");
                continue;
            }

            // Parse the dependency's code to extract actual type names
            var availableTypes = ExtractTypeNames(depTask.GeneratedCode);
            
            // Check if each consumed type exists in the dependency
            foreach (var typeName in consumedTypes)
            {
                if (!availableTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Expected type '{typeName}' not found in dependency '{depId}'. Available types: {string.Join(", ", availableTypes)}");
                }
                else
                {
                    Console.WriteLine($"   ✓ Type '{typeName}' found in dependency '{depId}'");
                }
            }
        }

        if (result.IsValid && task.ConsumedTypes.Any())
        {
            Console.WriteLine($"✓ All dependencies validated for task '{task.Id}'");
        }
        else if (!result.IsValid)
        {
            Console.WriteLine($"✗ Dependency validation failed for task '{task.Id}'");
        }

        return result;
    }

    /// <summary>
    /// Validates that multiple code snippets can work together by checking namespace conflicts and type compatibility.
    /// </summary>
    public ValidationResult ValidateIntegration(List<TaskNode> tasks)
    {
        var result = new ValidationResult { IsValid = true };
        
        Console.WriteLine("\n🔗 Validating code integration across tasks...");

        var typesByNamespace = new Dictionary<string, HashSet<string>>();
        
        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.GeneratedCode))
            {
                continue;
            }

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(task.GeneratedCode);
                var root = syntaxTree.GetRoot();
                
                var namespaceDecls = root.DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>();
                
                foreach (var namespaceDecl in namespaceDecls)
                {
                    var namespaceName = namespaceDecl.Name.ToString();
                    
                    if (!typesByNamespace.ContainsKey(namespaceName))
                    {
                        typesByNamespace[namespaceName] = new HashSet<string>();
                    }
                    
                    var typeDecls = namespaceDecl.DescendantNodes()
                        .OfType<TypeDeclarationSyntax>();
                    
                    foreach (var typeDecl in typeDecls)
                    {
                        var typeName = typeDecl.Identifier.Text;
                        
                        if (typesByNamespace[namespaceName].Contains(typeName))
                        {
                            result.Warnings.Add($"Duplicate type '{typeName}' found in namespace '{namespaceName}' (task: {task.Id})");
                        }
                        else
                        {
                            typesByNamespace[namespaceName].Add(typeName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to analyze task '{task.Id}' for integration: {ex.Message}");
            }
        }

        Console.WriteLine($"   Found {typesByNamespace.Sum(kvp => kvp.Value.Count)} types across {typesByNamespace.Count} namespaces");
        
        if (result.IsValid)
        {
            Console.WriteLine("✓ Code integration validation passed");
        }

        return result;
    }

    /// <summary>
    /// Extracts all type names (classes, interfaces, enums, records) from code.
    /// </summary>
    private HashSet<string> ExtractTypeNames(string code)
    {
        var typeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot();
            
            var typeDeclarations = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>();
            
            foreach (var typeDecl in typeDeclarations)
            {
                typeNames.Add(typeDecl.Identifier.Text);
            }
        }
        catch
        {
            // If parsing fails, return empty set
        }
        
        return typeNames;
    }

    /// <summary>
    /// Runs basic linting checks on the code.
    /// </summary>
    public ValidationResult LintCode(string code)
    {
        var result = new ValidationResult { IsValid = true };

        // Basic linting rules
        if (string.IsNullOrWhiteSpace(code))
        {
            result.IsValid = false;
            result.Errors.Add("Code is empty");
            return result;
        }

        // Check for common issues
        if (code.Contains("TODO") || code.Contains("FIXME"))
        {
            result.Warnings.Add("Code contains TODO or FIXME comments");
        }

        if (!code.Contains("namespace"))
        {
            result.Warnings.Add("Code does not define a namespace");
        }

        // Check for excessive line length
        var lines = code.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 120)
            {
                result.Warnings.Add($"Line {i + 1} exceeds 120 characters");
            }
        }

        return result;
    }

    /// <summary>
    /// Adds a custom namespace-to-assembly mapping at runtime.
    /// Useful when dealing with specific NuGet packages or custom assemblies.
    /// </summary>
    public void AddAssemblyMapping(string namespaceName, string assemblyName)
    {
        _assemblyManager.AddMapping(namespaceName, assemblyName);
    }
}
