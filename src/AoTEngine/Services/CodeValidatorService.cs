using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;
using System.Runtime.Loader;

namespace AoTEngine.Services;

/// <summary>
/// Service for validating generated C# code.
/// </summary>
public class CodeValidatorService
{
    private readonly List<MetadataReference> _references;

    public CodeValidatorService()
    {
        // Add common references with error handling
        _references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
        };

        // Try to load additional assemblies with error handling
        TryAddAssemblyReference("System.Runtime");
        TryAddAssemblyReference("System.Collections");
        TryAddAssemblyReference("System.Text.RegularExpressions");
        TryAddAssemblyReference("netstandard");
    }

    private void TryAddAssemblyReference(string assemblyName)
    {
        try
        {
            var assembly = Assembly.Load(assemblyName);
            _references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
        catch (FileNotFoundException)
        {
            // Assembly not available in this runtime environment
            // Continue without it - core references should be sufficient
        }
        catch (Exception ex)
        {
            // Log but don't fail on assembly loading errors
            Console.WriteLine($"Warning: Could not load assembly {assemblyName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates C# code by attempting to compile it.
    /// </summary>
    public async Task<ValidationResult> ValidateCodeAsync(string code)
    {
        return await Task.Run(() => ValidateCode(code));
    }

    private ValidationResult ValidateCode(string code)
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            // Parse the code
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            
            // Check for syntax errors
            var syntaxDiagnostics = syntaxTree.GetDiagnostics();
            foreach (var diagnostic in syntaxDiagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    result.Errors.Add($"Syntax Error at {diagnostic.Location.GetLineSpan().StartLinePosition}: {diagnostic.GetMessage()}");
                    result.IsValid = false;
                }
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                {
                    result.Warnings.Add($"Warning at {diagnostic.Location.GetLineSpan().StartLinePosition}: {diagnostic.GetMessage()}");
                }
            }

            if (!result.IsValid)
            {
                return result;
            }

            // Try to compile
            var assemblyName = $"GeneratedAssembly_{Guid.NewGuid():N}";
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            EmitResult emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                result.IsValid = false;
                foreach (var diagnostic in emitResult.Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                    {
                        result.Errors.Add($"Compilation Error: {diagnostic.GetMessage()}");
                    }
                    else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                    {
                        result.Warnings.Add($"Compilation Warning: {diagnostic.GetMessage()}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation exception: {ex.Message}");
        }

        return result;
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
}
