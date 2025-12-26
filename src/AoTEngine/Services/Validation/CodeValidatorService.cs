using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace AoTEngine.Services;

/// <summary>
/// Service for validating generated C# code.
/// This is the main partial class containing core fields, constructor, and primary validation methods.
/// </summary>
/// <remarks>
/// This class is split into multiple partial class files for maintainability:
/// - CodeValidatorService.cs (this file): Core fields, constructor, and main validation method
/// - CodeValidatorService.Compilation.cs: Compilation and assembly resolution methods
/// - CodeValidatorService.Integration.cs: Integration validation, dependency checking, and linting
/// </remarks>
public partial class CodeValidatorService
{
    private readonly AssemblyReferenceManager _assemblyManager;
    private readonly IConfiguration? _configuration;
    private const int MaxAssemblyRetries = 3;

    public CodeValidatorService(IConfiguration? configuration = null)
    {
        _configuration = configuration;
        _assemblyManager = new AssemblyReferenceManager(configuration);
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
            Console.WriteLine("\n🔍 Validating generated code...");
            
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

            // Get dynamic references based on code's using directives
            Console.WriteLine("📦 Resolving assembly references...");
            var references = _assemblyManager.GetReferencesForCode(code);
            Console.WriteLine($"   Loaded {references.Count} initial assembly references");

            // Try to compile with automatic missing assembly detection and retry
            result = AttemptCompilationWithAssemblyResolution(code, syntaxTree, references);
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation exception: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Loads assembly mapping configuration.
    /// </summary>
    private AssemblyMappingConfig LoadAssemblyConfig()
    {
        var config = new AssemblyMappingConfig();
        
        if (_configuration != null)
        {
            _configuration.GetSection("AssemblyMappings").Bind(config);
        }

        return config;
    }

    /// <summary>
    /// Gets the default metadata references for code compilation.
    /// </summary>
    /// <returns>List of metadata references.</returns>
    public List<MetadataReference> GetDefaultReferences()
    {
        // Return a basic set of references for validation
        return _assemblyManager.GetReferencesForCode("");
    }
}
