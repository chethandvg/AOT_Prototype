using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text;

namespace AoTEngine.Services.AI;

/// <summary>
/// Service for compressing context while preserving critical information.
/// Uses Roslyn for accurate code extraction and verification.
/// </summary>
public class ContextCompressionServiceV2
{
    private readonly int _maxContextTokens;
    private readonly int _compressionThreshold;
    private readonly bool _enableColdStorage;
    private readonly string? _coldStoragePath;

    public ContextCompressionServiceV2(
        int maxContextTokens = 8000,
        int compressionThreshold = 6000,
        bool enableColdStorage = true,
        string? coldStoragePath = null)
    {
        _maxContextTokens = maxContextTokens;
        _compressionThreshold = compressionThreshold;
        _enableColdStorage = enableColdStorage;
        _coldStoragePath = coldStoragePath ?? Path.Combine(Path.GetTempPath(), "aot_cold_storage");

        if (_enableColdStorage && !Directory.Exists(_coldStoragePath))
        {
            Directory.CreateDirectory(_coldStoragePath);
        }
    }

    /// <summary>
    /// Compresses context if it exceeds the threshold.
    /// </summary>
    public async Task<CompressedContext> CompressContextAsync(string context, List<string>? criticalTypes = null)
    {
        var originalTokenCount = EstimateTokenCount(context);

        if (originalTokenCount <= _compressionThreshold)
        {
            return new CompressedContext
            {
                CompressedText = context,
                OriginalText = context,
                CompressionRatio = 1.0,
                OriginalTokenCount = originalTokenCount,
                CompressedTokenCount = originalTokenCount,
                IsLossy = false,
                VerificationPassed = true
            };
        }

        // Extract critical information using Roslyn
        var compressed = await ExtractCriticalInformationAsync(context, criticalTypes ?? new List<string>());
        var compressedTokenCount = EstimateTokenCount(compressed);

        // Verify compression didn't lose critical information
        var verificationPassed = await VerifyCompressionAsync(context, compressed, criticalTypes ?? new List<string>());

        string? coldStoragePath = null;
        if (_enableColdStorage)
        {
            coldStoragePath = await StoreToColdStorageAsync(context);
        }

        return new CompressedContext
        {
            CompressedText = compressed,
            OriginalText = verificationPassed ? null : context, // Keep original if verification failed
            CompressionRatio = (double)compressedTokenCount / originalTokenCount,
            OriginalTokenCount = originalTokenCount,
            CompressedTokenCount = compressedTokenCount,
            IsLossy = compressedTokenCount < originalTokenCount,
            VerificationPassed = verificationPassed,
            ColdStoragePath = coldStoragePath
        };
    }

    /// <summary>
    /// Extracts critical information from code using Roslyn.
    /// </summary>
    private async Task<string> ExtractCriticalInformationAsync(string code, List<string> criticalTypes)
    {
        return await Task.Run(() =>
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();
                var sb = new StringBuilder();

                // Extract namespaces
                var namespaces = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>();
                foreach (var ns in namespaces)
                {
                    sb.AppendLine($"namespace {ns.Name};");
                }

                // Extract using directives
                var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
                foreach (var u in usings.Take(10)) // Limit usings
                {
                    sb.AppendLine(u.ToString());
                }

                // Extract type signatures (interfaces, classes, enums)
                var typeDeclarations = root.DescendantNodes()
                    .Where(n => n is ClassDeclarationSyntax || n is InterfaceDeclarationSyntax || n is EnumDeclarationSyntax);

                foreach (var typeDecl in typeDeclarations)
                {
                    if (typeDecl is ClassDeclarationSyntax classDecl)
                    {
                        sb.AppendLine($"// {classDecl.Modifiers} class {classDecl.Identifier}");
                        // Extract public members only
                        foreach (var member in classDecl.Members.OfType<MethodDeclarationSyntax>()
                            .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword)))
                        {
                            sb.AppendLine($"//   {member.ReturnType} {member.Identifier}({string.Join(", ", member.ParameterList.Parameters)})");
                        }
                    }
                    else if (typeDecl is InterfaceDeclarationSyntax interfaceDecl)
                    {
                        sb.AppendLine(interfaceDecl.ToString()); // Keep full interface
                    }
                    else if (typeDecl is EnumDeclarationSyntax enumDecl)
                    {
                        sb.AppendLine(enumDecl.ToString()); // Keep full enum
                    }
                }

                return sb.ToString();
            }
            catch
            {
                // If Roslyn parsing fails, use simple compression
                var maxLength = Math.Min(code.Length, _maxContextTokens * 4);
                return code.Substring(0, maxLength);
            }
        });
    }

    /// <summary>
    /// Verifies that compression didn't lose critical information.
    /// </summary>
    private async Task<bool> VerifyCompressionAsync(string original, string compressed, List<string> criticalTypes)
    {
        return await Task.Run(() =>
        {
            // Check if critical types are still present
            foreach (var type in criticalTypes)
            {
                if (!compressed.Contains(type))
                {
                    return false;
                }
            }

            // Basic verification: ensure interfaces and enums are preserved
            var originalTree = CSharpSyntaxTree.ParseText(original);
            var compressedTree = CSharpSyntaxTree.ParseText(compressed);

            var originalInterfaces = originalTree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>().Count();
            var compressedInterfaces = compressedTree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>().Count();

            // Should preserve all interfaces - require 100% preservation for critical contracts
            return compressedInterfaces >= originalInterfaces;
        });
    }

    /// <summary>
    /// Stores original context to cold storage.
    /// </summary>
    private async Task<string> StoreToColdStorageAsync(string context)
    {
        var hash = ComputeHash(context);
        var filename = $"{hash}.txt";
        var path = Path.Combine(_coldStoragePath!, filename);

        await File.WriteAllTextAsync(path, context);
        return path;
    }

    /// <summary>
    /// Retrieves context from cold storage.
    /// </summary>
    public async Task<string?> RetrieveFromColdStorageAsync(string coldStoragePath)
    {
        if (!File.Exists(coldStoragePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(coldStoragePath);
    }

    /// <summary>
    /// Estimates token count (simple approximation: ~4 chars per token).
    /// </summary>
    private int EstimateTokenCount(string text)
    {
        return text.Length / 4;
    }

    /// <summary>
    /// Computes SHA256 hash of text.
    /// </summary>
    private string ComputeHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
