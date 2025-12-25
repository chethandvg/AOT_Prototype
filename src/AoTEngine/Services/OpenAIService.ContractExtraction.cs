using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing contract extraction methods for code analysis.
/// </summary>
public partial class OpenAIService
{
    /// <summary>
    /// Extracts type signatures, interfaces, and public contracts from generated code.
    /// This provides a lightweight contract for dependent tasks to reference.
    /// </summary>
    private string ExtractTypeContract(string code)
    {
        var contractBuilder = new System.Text.StringBuilder();
        
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot();
            
            // Extract using directives
            var usingDirectives = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(u => u.ToString().Trim())
                .Distinct();
            
            foreach (var usingDirective in usingDirectives)
            {
                contractBuilder.AppendLine(usingDirective);
            }
            
            if (usingDirectives.Any())
            {
                contractBuilder.AppendLine();
            }
            
            // Extract namespace declarations
            var namespaceDeclarations = root.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>();
            
            foreach (var namespaceDecl in namespaceDeclarations)
            {
                contractBuilder.AppendLine($"namespace {namespaceDecl.Name};");
                contractBuilder.AppendLine();
                
                // Extract type declarations (classes, interfaces, enums, records)
                var typeDeclarations = namespaceDecl.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>();
                
                foreach (var typeDecl in typeDeclarations)
                {
                    // Extract modifiers, keyword, identifier, and base types
                    var modifiers = string.Join(" ", typeDecl.Modifiers.Select(m => m.Text));
                    var keyword = typeDecl.Keyword.Text;
                    var identifier = typeDecl.Identifier.Text;
                    var baseList = typeDecl.BaseList?.ToString() ?? "";
                    
                    contractBuilder.AppendLine($"{modifiers} {keyword} {identifier}{baseList}");
                    contractBuilder.AppendLine("{");
                    
                    // Extract public members (properties, methods, events)
                    var publicMembers = typeDecl.Members
                        .Where(m => m.Modifiers.Any(mod => mod.Text == "public"));
                    
                    foreach (var member in publicMembers)
                    {
                        var memberSignature = ExtractMemberSignature(member);
                        if (!string.IsNullOrEmpty(memberSignature))
                        {
                            contractBuilder.AppendLine($"    {memberSignature}");
                        }
                    }
                    
                    contractBuilder.AppendLine("}");
                    contractBuilder.AppendLine();
                }
            }
        }
        catch
        {
            // Fallback: return a comment if parsing fails
            return $"// Contract extraction failed - full code required\n// Code length: {code.Length} chars";
        }
        
        return contractBuilder.ToString();
    }

    /// <summary>
    /// Extracts the signature of a class member (property, method, event, etc.)
    /// </summary>
    private string ExtractMemberSignature(MemberDeclarationSyntax member)
    {
        return member switch
        {
            PropertyDeclarationSyntax property => 
                $"{property.Modifiers} {property.Type} {property.Identifier} {{ {string.Join(" ", property.AccessorList?.Accessors.Select(a => a.Keyword.Text + ";") ?? [])} }}",
            
            MethodDeclarationSyntax method =>
                $"{method.Modifiers} {method.ReturnType} {method.Identifier}{method.ParameterList};",
            
            FieldDeclarationSyntax field =>
                $"{field.Modifiers} {field.Declaration};",
            
            EventDeclarationSyntax eventDecl =>
                $"{eventDecl.Modifiers} event {eventDecl.Type} {eventDecl.Identifier};",
            
            ConstructorDeclarationSyntax ctor =>
                $"{ctor.Modifiers} {ctor.Identifier}{ctor.ParameterList};",
            
            _ => member.ToString().Split('{')[0].Trim() + ";"
        };
    }
}
