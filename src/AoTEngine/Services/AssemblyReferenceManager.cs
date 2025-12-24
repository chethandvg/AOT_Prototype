using AoTEngine.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AoTEngine.Services;

/// <summary>
/// Configuration for assembly mappings.
/// </summary>
public class AssemblyMappingConfig
{
    public Dictionary<string, string> NamespaceToAssemblyMappings { get; set; } = new();
    public List<string> CommonAssemblies { get; set; } = new();
    public Dictionary<string, string> NuGetPackages { get; set; } = new();
}

/// <summary>
/// Manages dynamic assembly reference resolution.
/// </summary>
public class AssemblyReferenceManager
{
    private readonly Dictionary<string, string> _namespaceToAssemblyMap;
    private readonly List<string> _commonAssemblies;
    private readonly HashSet<string> _loadedAssemblies;
    private readonly List<MetadataReference> _baseReferences;

    public AssemblyReferenceManager(IConfiguration? configuration = null)
    {
        _loadedAssemblies = new HashSet<string>();
        _baseReferences = new List<MetadataReference>();

        // Load configuration
        if (configuration != null)
        {
            var config = new AssemblyMappingConfig();
            configuration.GetSection("AssemblyMappings").Bind(config);
            _namespaceToAssemblyMap = config.NamespaceToAssemblyMappings;
            _commonAssemblies = config.CommonAssemblies;
        }
        else
        {
            // Default mappings if no configuration
            _namespaceToAssemblyMap = GetDefaultMappings();
            _commonAssemblies = GetDefaultCommonAssemblies();
        }

        InitializeBaseReferences();
    }

    private Dictionary<string, string> GetDefaultMappings()
    {
        return new Dictionary<string, string>
        {
            { "System.Text.Json", "System.Text.Json" },
            { "System.Text.Json.Serialization", "System.Text.Json" },
            { "System.Net.Http", "System.Net.Http" },
            { "System.Net.Http.Json", "System.Net.Http.Json" },
            { "System.Net", "System.Net.Primitives" },
            { "System.IO.Pipelines", "System.IO.Pipelines" },
            { "System.Threading.Channels", "System.Threading.Channels" },
            { "System.Memory", "System.Memory" },
            { "System.Buffers", "System.Buffers" },
            { "System.Data", "System.Data" },
            { "System.Data.Common", "System.Data.Common" },
            { "System.Xml", "System.Xml" },
            { "System.Xml.Linq", "System.Xml.Linq" },
            { "System.ComponentModel.DataAnnotations", "System.ComponentModel.Annotations" },
            { "Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore" },
            { "Newtonsoft.Json", "Newtonsoft.Json" }
        };
    }

    private List<string> GetDefaultCommonAssemblies()
    {
        return new List<string>
        {
            "System.Runtime",
            "System.Collections",
            "System.Text.RegularExpressions",
            "System.Linq",
            "System.Net.Primitives",
            "System.Private.Uri",
            "System.IO.Pipelines",
            "System.Threading.Channels",
            "System.Memory",
            "System.Buffers"
        };
    }

    private void InitializeBaseReferences()
    {
        // Add core references by type (guaranteed to exist)
        _baseReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        _baseReferences.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
        _baseReferences.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location));
        
        // Add Uri type assembly
        TryAddTypeReference(typeof(Uri));
        
        // Add HttpStatusCode type assembly
        TryAddTypeReference(typeof(System.Net.HttpStatusCode));

        // Load common assemblies
        foreach (var assemblyName in _commonAssemblies)
        {
            TryAddAssemblyReference(assemblyName, _baseReferences);
        }
    }

    private void TryAddTypeReference(Type type)
    {
        try
        {
            var reference = MetadataReference.CreateFromFile(type.Assembly.Location);
            if (!_baseReferences.Any(r => r.Display == reference.Display))
            {
                _baseReferences.Add(reference);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not load assembly for type {type.FullName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a namespace-to-assembly mapping at runtime.
    /// </summary>
    public void AddMapping(string namespaceName, string assemblyName)
    {
        _namespaceToAssemblyMap[namespaceName] = assemblyName;
    }

    /// <summary>
    /// Gets references dynamically based on code's using directives.
    /// </summary>
    public List<MetadataReference> GetReferencesForCode(string code)
    {
        var references = new List<MetadataReference>(_baseReferences);
        var codeSpecificAssemblies = new HashSet<string>();

        // Extract namespaces from using directives
        var namespaces = ExtractUsingDirectives(code);

        foreach (var ns in namespaces)
        {
            ResolveNamespace(ns, references, codeSpecificAssemblies);
        }

        return references;
    }

    private void ResolveNamespace(string ns, List<MetadataReference> references, HashSet<string> loaded)
    {
        // Check explicit mappings first
        if (_namespaceToAssemblyMap.TryGetValue(ns, out var assemblyName))
        {
            if (!loaded.Contains(assemblyName))
            {
                TryAddAssemblyReference(assemblyName, references);
                loaded.Add(assemblyName);
            }
            return;
        }

        // Try progressively shorter namespace segments
        // e.g., System.Text.Json.Serialization -> System.Text.Json -> System.Text -> System
        var parts = ns.Split('.');
        for (int i = parts.Length; i > 0; i--)
        {
            var testNamespace = string.Join(".", parts.Take(i));
            
            if (_namespaceToAssemblyMap.TryGetValue(testNamespace, out var mappedAssembly))
            {
                if (!loaded.Contains(mappedAssembly))
                {
                    TryAddAssemblyReference(mappedAssembly, references);
                    loaded.Add(mappedAssembly);
                }
                return;
            }

            // Try loading assembly with the same name
            if (!loaded.Contains(testNamespace))
            {
                if (TryAddAssemblyReference(testNamespace, references))
                {
                    loaded.Add(testNamespace);
                    return;
                }
            }
        }
    }

    private List<string> ExtractUsingDirectives(string code)
    {
        var namespaces = new List<string>();
        var usingPattern = @"using\s+((?:[\w\.]+)(?:\s*=\s*[\w\.]+)?)\s*;";
        var matches = Regex.Matches(code, usingPattern);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var usingStatement = match.Groups[1].Value;
                
                // Handle static using
                if (usingStatement.StartsWith("static "))
                {
                    usingStatement = usingStatement.Substring(7);
                }

                // Handle alias (e.g., using Json = System.Text.Json;)
                if (usingStatement.Contains("="))
                {
                    var parts = usingStatement.Split('=');
                    namespaces.Add(parts[1].Trim());
                }
                else
                {
                    namespaces.Add(usingStatement.Trim());
                }
            }
        }

        return namespaces;
    }

    private bool TryAddAssemblyReference(string assemblyName, List<MetadataReference> references)
    {
        try
        {
            var assembly = Assembly.Load(assemblyName);
            var reference = MetadataReference.CreateFromFile(assembly.Location);
            
            // Avoid duplicates
            if (!references.Any(r => r.Display == reference.Display))
            {
                references.Add(reference);
                Console.WriteLine($"? Loaded assembly reference: {assemblyName}");
                return true;
            }
            return true;
        }
        catch (FileNotFoundException)
        {
            // Assembly not found - this is normal for some namespaces
            return false;
        }
        catch (Exception ex)
        {
            // Log but don't fail
            Console.WriteLine($"  Could not load assembly {assemblyName}: {ex.Message}");
            return false;
        }
    }
}
