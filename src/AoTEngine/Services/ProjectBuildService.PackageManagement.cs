using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AoTEngine.Models;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing package management methods.
/// </summary>
public partial class ProjectBuildService
{
    /// <summary>
    /// Extracts required package names from all tasks, including packages specified in RequiredPackages
    /// and packages detected from using statements in generated code.
    /// Returns only package names - versions will be resolved by OpenAI.
    /// </summary>
    private List<string> ExtractRequiredPackageNamesFromTasks(List<TaskNode> tasks)
    {
        var packageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Common mappings from using statements to NuGet package names
        // Note: System.Text.Json is included in .NET 9 BCL and doesn't need a package reference
        var usingToPackageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Microsoft.Extensions packages
            { "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection" },
            { "Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging" },
            { "Microsoft.Extensions.Configuration", "Microsoft.Extensions.Configuration" },
            { "Microsoft.Extensions.Hosting", "Microsoft.Extensions.Hosting" },
            { "Microsoft.Extensions.Http", "Microsoft.Extensions.Http" },
            { "Microsoft.Extensions.Options", "Microsoft.Extensions.Options" },
            { "Microsoft.Extensions.Caching.Memory", "Microsoft.Extensions.Caching.Memory" },
            { "Microsoft.Extensions.Caching.Abstractions", "Microsoft.Extensions.Caching.Abstractions" },
            
            // Entity Framework Core
            { "Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore" },
            
            // JSON (Newtonsoft.Json requires package, System.Text.Json is in BCL)
            { "Newtonsoft.Json", "Newtonsoft.Json" },
            
            // Popular libraries
            { "Dapper", "Dapper" },
            { "AutoMapper", "AutoMapper" },
            { "FluentValidation", "FluentValidation" },
            { "Serilog", "Serilog" },
            { "Polly", "Polly" },
            
            // Roslyn
            { "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp" },
        };

        foreach (var task in tasks.Where(t => !string.IsNullOrWhiteSpace(t.GeneratedCode)))
        {
            // Add explicitly specified packages from RequiredPackages
            if (task.RequiredPackages != null)
            {
                foreach (var package in task.RequiredPackages)
                {
                    // Parse package specification (e.g., "Newtonsoft.Json" or "Newtonsoft.Json:13.0.4")
                    var parts = package.Split(':');
                    var packageName = parts[0].Trim();
                    packageNames.Add(packageName);
                }
            }

            // Extract packages from using statements in generated code
            var usings = ExtractUsingStatements(task.GeneratedCode);
            foreach (var usingStatement in usings)
            {
                // Check if this using statement maps to a known package
                foreach (var mapping in usingToPackageMap)
                {
                    if (usingStatement.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        packageNames.Add(mapping.Value);
                        break;
                    }
                }
            }
        }

        return packageNames.ToList();
    }

    /// <summary>
    /// Gets fallback package versions when OpenAI is not available.
    /// </summary>
    private Dictionary<string, string> GetFallbackPackageVersions(List<string> packageNames)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var packageName in packageNames)
        {
            result[packageName] = GetDefaultVersionForPackage(packageName);
        }
        return result;
    }

    /// <summary>
    /// Extracts using statements from code, including global usings and using aliases.
    /// </summary>
    private List<string> ExtractUsingStatements(string code)
    {
        var usings = new List<string>();
        // Pattern breakdown:
        // ^                          - Start of line (Multiline mode)
        // (?:global\s+)?             - Optional "global " prefix
        // using\s+                   - Required "using " keyword
        // (?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?  - Optional alias (e.g., "Json = ")
        // ([A-Za-z_][A-Za-z0-9_.]+)  - Capture group: namespace identifier
        // \s*;                       - Ending semicolon with optional whitespace
        var regex = new Regex(@"^(?:global\s+)?using\s+(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?([A-Za-z_][A-Za-z0-9_.]+)\s*;", RegexOptions.Multiline);
        var matches = regex.Matches(code);

        foreach (Match match in matches.Where(m => m.Groups.Count > 1))
        {
            usings.Add(match.Groups[1].Value);
        }

        return usings;
    }

    /// <summary>
    /// Adds package references to the .csproj file.
    /// </summary>
    private Task AddPackageReferencesToCsprojAsync(string csprojPath, Dictionary<string, string> packages)
    {
        if (!File.Exists(csprojPath))
        {
            throw new FileNotFoundException($"Project file not found: {csprojPath}");
        }

        var doc = XDocument.Load(csprojPath);
        var projectElement = doc.Root;

        if (projectElement == null)
        {
            throw new InvalidOperationException("Invalid .csproj file structure");
        }

        // Find or create ItemGroup for package references
        var itemGroup = projectElement.Elements("ItemGroup")
            .FirstOrDefault(ig => ig.Elements("PackageReference").Any());

        if (itemGroup == null)
        {
            itemGroup = new XElement("ItemGroup");
            projectElement.Add(itemGroup);
        }

        // Get existing package references to avoid duplicates
        var existingPackages = itemGroup.Elements("PackageReference")
            .Select(pr => pr.Attribute("Include")?.Value)
            .Where(v => v != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Add new package references - use explicit Where filter
        foreach (var package in packages.Where(p => !existingPackages.Contains(p.Key)))
        {
            // If version is "latest", try to use a sensible default version or omit
            // Using a concrete version is more reliable than "*" for reproducible builds
            var version = package.Value;
            if (version == "latest")
            {
                // Try to get a default version from our known mappings
                version = GetDefaultVersionForPackage(package.Key);
            }
            
            var packageRef = new XElement("PackageReference",
                new XAttribute("Include", package.Key),
                new XAttribute("Version", version));
            itemGroup.Add(packageRef);
        }

        // Save with proper formatting to maintain readability
        doc.Save(csprojPath, SaveOptions.None);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a sensible default version for a package when "latest" is specified.
    /// Uses the shared KnownPackageVersions for consistency.
    /// </summary>
    private string GetDefaultVersionForPackage(string packageName)
    {
        return KnownPackageVersions.GetVersionWithFallback(packageName);
    }
}
