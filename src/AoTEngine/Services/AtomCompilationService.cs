using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Service for performing design-time compilation checks after each atom (code file) is generated.
/// This enables early detection of errors before the full batch validation.
/// </summary>
public class AtomCompilationService
{
    private readonly AssemblyReferenceManager _assemblyManager;
    private readonly ContractCatalog? _contractCatalog;
    private readonly TypeRegistry _typeRegistry;
    private readonly SymbolTable _symbolTable;

    /// <summary>
    /// Classification of compilation diagnostics.
    /// </summary>
    public enum DiagnosticCategory
    {
        /// <summary>Symbol collision (duplicate type/member).</summary>
        SymbolCollision,
        
        /// <summary>Missing interface member implementation.</summary>
        MissingInterfaceMember,
        
        /// <summary>Wrong return type or signature mismatch.</summary>
        SignatureMismatch,
        
        /// <summary>Missing enum member reference.</summary>
        MissingEnumMember,
        
        /// <summary>Illegal inheritance from sealed class.</summary>
        IllegalInheritance,
        
        /// <summary>Missing using directive.</summary>
        MissingUsing,
        
        /// <summary>Ambiguous type reference.</summary>
        AmbiguousReference,
        
        /// <summary>Other compilation error.</summary>
        Other
    }

    public AtomCompilationService(
        AssemblyReferenceManager assemblyManager,
        ContractCatalog? contractCatalog = null,
        TypeRegistry? typeRegistry = null,
        SymbolTable? symbolTable = null)
    {
        _assemblyManager = assemblyManager;
        _contractCatalog = contractCatalog;
        _typeRegistry = typeRegistry ?? new TypeRegistry();
        _symbolTable = symbolTable ?? new SymbolTable();
    }

    /// <summary>
    /// Performs a quick compilation check on generated code with contract context.
    /// </summary>
    /// <param name="code">The generated code to check.</param>
    /// <param name="contractCode">Code from frozen contracts (for reference resolution).</param>
    /// <param name="dependencyCode">Code from dependent tasks.</param>
    /// <returns>Compilation result with classified diagnostics.</returns>
    public AtomCompilationResult CompileAtom(
        string code,
        string? contractCode = null,
        string? dependencyCode = null)
    {
        var result = new AtomCompilationResult();

        try
        {
            var syntaxTrees = new List<SyntaxTree>();

            // Add contract code if available
            if (!string.IsNullOrEmpty(contractCode))
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(contractCode, path: "Contracts.cs"));
            }

            // Add dependency code if available
            if (!string.IsNullOrEmpty(dependencyCode))
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(dependencyCode, path: "Dependencies.cs"));
            }

            // Add the atom code
            var atomTree = CSharpSyntaxTree.ParseText(code, path: "Atom.cs");
            syntaxTrees.Add(atomTree);

            // Check for syntax errors first
            var syntaxDiagnostics = atomTree.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (syntaxDiagnostics.Any())
            {
                foreach (var diag in syntaxDiagnostics)
                {
                    result.ClassifiedDiagnostics.Add(new ClassifiedDiagnostic
                    {
                        Diagnostic = diag,
                        Category = DiagnosticCategory.Other,
                        Message = diag.GetMessage(),
                        IsAutoFixable = false
                    });
                }
                result.Success = false;
                return result;
            }

            // Get references
            var references = _assemblyManager.GetReferencesForCode(code);

            // Compile
            var compilation = CSharpCompilation.Create(
                $"AtomCheck_{Guid.NewGuid():N}",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (!diagnostics.Any())
            {
                result.Success = true;
                return result;
            }

            // Classify diagnostics
            foreach (var diagnostic in diagnostics)
            {
                var classified = ClassifyDiagnostic(diagnostic, code);
                result.ClassifiedDiagnostics.Add(classified);
            }

            result.Success = false;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ClassifiedDiagnostics.Add(new ClassifiedDiagnostic
            {
                Category = DiagnosticCategory.Other,
                Message = $"Compilation exception: {ex.Message}",
                IsAutoFixable = false
            });
        }

        return result;
    }

    /// <summary>
    /// Classifies a diagnostic into a category for targeted fixing.
    /// </summary>
    private ClassifiedDiagnostic ClassifyDiagnostic(Diagnostic diagnostic, string code)
    {
        var message = diagnostic.GetMessage();
        var diagId = diagnostic.Id;

        var classified = new ClassifiedDiagnostic
        {
            Diagnostic = diagnostic,
            Message = message
        };

        // CS0101: The namespace already contains a definition
        // CS0111: Type already defines a member with same parameter types
        if (diagId == "CS0101" || diagId == "CS0111")
        {
            classified.Category = DiagnosticCategory.SymbolCollision;
            classified.IsAutoFixable = true;
            classified.SuggestedFix = "Remove duplicate definition or rename the type/member";
        }
        // CS0104: Ambiguous reference
        else if (diagId == "CS0104")
        {
            classified.Category = DiagnosticCategory.AmbiguousReference;
            classified.IsAutoFixable = true;
            classified.SuggestedFix = "Use fully qualified type name";
        }
        // CS0246: The type or namespace name could not be found
        // CS0234: The type or namespace name does not exist
        else if (diagId == "CS0246" || diagId == "CS0234")
        {
            classified.Category = DiagnosticCategory.MissingUsing;
            classified.IsAutoFixable = true;
            classified.SuggestedFix = "Add missing using directive";
        }
        // CS0535: Does not implement interface member
        else if (diagId == "CS0535")
        {
            classified.Category = DiagnosticCategory.MissingInterfaceMember;
            classified.IsAutoFixable = true;
            classified.SuggestedFix = ExtractMissingMemberFix(message);
        }
        // CS0534: Does not implement inherited abstract member
        else if (diagId == "CS0534")
        {
            classified.Category = DiagnosticCategory.MissingInterfaceMember;
            classified.IsAutoFixable = true;
            classified.SuggestedFix = "Implement abstract method with exact signature";
        }
        // CS0508: Return type must be X to match overridden member
        // CS0115: No suitable method found to override
        else if (diagId == "CS0508" || diagId == "CS0115")
        {
            classified.Category = DiagnosticCategory.SignatureMismatch;
            classified.IsAutoFixable = true;
            classified.SuggestedFix = "Fix return type or method signature to match base/interface";
        }
        // CS0509: Cannot derive from sealed type
        else if (diagId == "CS0509")
        {
            classified.Category = DiagnosticCategory.IllegalInheritance;
            classified.IsAutoFixable = true;
            classified.SuggestedFix = "Use composition instead of inheritance";
        }
        // CS0117: Does not contain a definition for (missing enum member, etc.)
        else if (diagId == "CS0117" && message.Contains("does not contain a definition"))
        {
            // Check if it's an enum member
            if (IsEnumMemberError(message))
            {
                classified.Category = DiagnosticCategory.MissingEnumMember;
                classified.IsAutoFixable = false;
                classified.SuggestedFix = "Use only defined enum members from the contract";
            }
            else
            {
                classified.Category = DiagnosticCategory.Other;
                classified.IsAutoFixable = false;
            }
        }
        else
        {
            classified.Category = DiagnosticCategory.Other;
            classified.IsAutoFixable = false;
        }

        return classified;
    }

    /// <summary>
    /// Extracts the missing member information from an error message.
    /// </summary>
    private string ExtractMissingMemberFix(string message)
    {
        // Try to extract the interface and member name
        // Example: "'MyClass' does not implement interface member 'IMyInterface.MyMethod()'"
        var match = System.Text.RegularExpressions.Regex.Match(
            message, 
            @"'([^']+)' does not implement interface member '([^']+)'");
        
        if (match.Success)
        {
            return $"Add implementation for: {match.Groups[2].Value}";
        }

        return "Implement all missing interface members";
    }

    /// <summary>
    /// Checks if an error is about a missing enum member.
    /// </summary>
    private bool IsEnumMemberError(string message)
    {
        // Check if the error mentions an enum type from our contract catalog
        if (_contractCatalog == null)
        {
            return false;
        }

        foreach (var enumContract in _contractCatalog.Enums)
        {
            if (message.Contains(enumContract.Name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates code against frozen contracts without full compilation.
    /// This is faster and catches contract violations early.
    /// </summary>
    public List<ContractViolation> ValidateAgainstContracts(string code)
    {
        var violations = new List<ContractViolation>();

        if (_contractCatalog == null || !_contractCatalog.IsFrozen)
        {
            return violations;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();

        // Check for duplicate type definitions
        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        
        foreach (var typeDecl in typeDeclarations)
        {
            var typeName = typeDecl.Identifier.Text;
            var ns = GetNamespace(typeDecl);
            var fqn = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";

            // Check if type is already defined in contracts
            var existingContract = _contractCatalog.GetContract(typeName);
            if (existingContract != null && existingContract.FullyQualifiedName != fqn)
            {
                violations.Add(new ContractViolation
                {
                    ViolationType = ContractViolationType.DuplicateType,
                    TypeName = typeName,
                    Message = $"Type '{typeName}' is already defined in contract at '{existingContract.FullyQualifiedName}'. Do not redefine it.",
                    Location = typeDecl.GetLocation()
                });
            }

            // Check for sealed type inheritance
            if (typeDecl.BaseList != null)
            {
                foreach (var baseType in typeDecl.BaseList.Types)
                {
                    var baseTypeName = baseType.Type.ToString();
                    var simpleBaseName = baseTypeName.Split('.').Last();

                    // Check if base type is sealed in contracts
                    var abstractContract = _contractCatalog.AbstractClasses
                        .FirstOrDefault(a => a.Name == simpleBaseName || a.FullyQualifiedName == baseTypeName);

                    if (abstractContract?.IsSealed == true)
                    {
                        violations.Add(new ContractViolation
                        {
                            ViolationType = ContractViolationType.SealedInheritance,
                            TypeName = typeName,
                            Message = $"Cannot inherit from sealed type '{baseTypeName}'. Use composition instead.",
                            Location = baseType.GetLocation()
                        });
                    }
                }
            }
        }

        // Check for invalid enum member usage
        var memberAccessExpressions = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
        
        foreach (var memberAccess in memberAccessExpressions)
        {
            var expression = memberAccess.Expression.ToString();
            var memberName = memberAccess.Name.ToString();

            // Check if this is an enum member access
            var enumContract = _contractCatalog.Enums.FirstOrDefault(e => 
                e.Name == expression || e.FullyQualifiedName == expression);

            if (enumContract != null)
            {
                if (!enumContract.Members.Any(m => m.Name == memberName))
                {
                    violations.Add(new ContractViolation
                    {
                        ViolationType = ContractViolationType.InvalidEnumMember,
                        TypeName = enumContract.Name,
                        Message = $"Enum member '{memberName}' does not exist in '{enumContract.Name}'. Valid members: {string.Join(", ", enumContract.Members.Select(m => m.Name))}",
                        Location = memberAccess.GetLocation()
                    });
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Generates a summary of compilation issues for the LLM.
    /// </summary>
    public string GenerateCompilationSummary(AtomCompilationResult result)
    {
        if (result.Success)
        {
            return "Compilation successful - no errors detected.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("COMPILATION ERRORS - Fix these before proceeding:");
        sb.AppendLine();

        // Group by category
        var byCategory = result.ClassifiedDiagnostics.GroupBy(d => d.Category);

        foreach (var group in byCategory)
        {
            sb.AppendLine($"## {group.Key}:");
            foreach (var diag in group)
            {
                sb.AppendLine($"  - {diag.Message}");
                if (!string.IsNullOrEmpty(diag.SuggestedFix))
                {
                    sb.AppendLine($"    → Fix: {diag.SuggestedFix}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string? GetNamespace(SyntaxNode node)
    {
        var nsDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return nsDecl?.Name.ToString();
    }
}

/// <summary>
/// Result of atom compilation check.
/// </summary>
public class AtomCompilationResult
{
    /// <summary>
    /// Whether compilation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Classified diagnostics with categories and suggested fixes.
    /// </summary>
    public List<ClassifiedDiagnostic> ClassifiedDiagnostics { get; set; } = new();

    /// <summary>
    /// Gets diagnostics by category.
    /// </summary>
    public IEnumerable<ClassifiedDiagnostic> GetByCategory(AtomCompilationService.DiagnosticCategory category)
    {
        return ClassifiedDiagnostics.Where(d => d.Category == category);
    }

    /// <summary>
    /// Gets all auto-fixable diagnostics.
    /// </summary>
    public IEnumerable<ClassifiedDiagnostic> GetAutoFixable()
    {
        return ClassifiedDiagnostics.Where(d => d.IsAutoFixable);
    }
}

/// <summary>
/// A classified diagnostic with category and fix suggestion.
/// </summary>
public class ClassifiedDiagnostic
{
    /// <summary>
    /// The original Roslyn diagnostic.
    /// </summary>
    public Diagnostic? Diagnostic { get; set; }

    /// <summary>
    /// The category of this diagnostic.
    /// </summary>
    public AtomCompilationService.DiagnosticCategory Category { get; set; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Whether this diagnostic can be auto-fixed.
    /// </summary>
    public bool IsAutoFixable { get; set; }

    /// <summary>
    /// Suggested fix for this diagnostic.
    /// </summary>
    public string SuggestedFix { get; set; } = string.Empty;
}

/// <summary>
/// A contract violation detected during validation.
/// </summary>
public class ContractViolation
{
    /// <summary>
    /// Type of contract violation.
    /// </summary>
    public ContractViolationType ViolationType { get; set; }

    /// <summary>
    /// The type name involved in the violation.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Violation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Location in source code.
    /// </summary>
    public Location? Location { get; set; }
}

/// <summary>
/// Types of contract violations.
/// </summary>
public enum ContractViolationType
{
    /// <summary>Duplicate type definition.</summary>
    DuplicateType,
    
    /// <summary>Invalid enum member usage.</summary>
    InvalidEnumMember,
    
    /// <summary>Inheritance from sealed type.</summary>
    SealedInheritance,
    
    /// <summary>Missing interface member.</summary>
    MissingInterfaceMember,
    
    /// <summary>Signature mismatch.</summary>
    SignatureMismatch
}
