namespace AoTEngine.Services;

/// <summary>
/// Provides known stable package versions compatible with .NET 9.
/// Used as fallback when OpenAI is not available or fails to provide versions.
/// </summary>
public static class KnownPackageVersions
{
    /// <summary>
    /// Known stable versions for common NuGet packages compatible with .NET 9.
    /// </summary>
    public static readonly Dictionary<string, string> Versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // JSON libraries
        { "Newtonsoft.Json", "13.0.4" },
        { "System.Text.Json", "9.0.0" },
        
        // Microsoft.Extensions packages
        { "Microsoft.Extensions.DependencyInjection", "9.0.0" },
        { "Microsoft.Extensions.DependencyInjection.Abstractions", "9.0.0" },
        { "Microsoft.Extensions.Logging", "9.0.0" },
        { "Microsoft.Extensions.Logging.Abstractions", "9.0.0" },
        { "Microsoft.Extensions.Logging.Console", "9.0.0" },
        { "Microsoft.Extensions.Configuration", "9.0.0" },
        { "Microsoft.Extensions.Configuration.Abstractions", "9.0.0" },
        { "Microsoft.Extensions.Configuration.Json", "9.0.0" },
        { "Microsoft.Extensions.Hosting", "9.0.0" },
        { "Microsoft.Extensions.Hosting.Abstractions", "9.0.0" },
        { "Microsoft.Extensions.Http", "9.0.0" },
        { "Microsoft.Extensions.Options", "9.0.0" },
        { "Microsoft.Extensions.Caching.Memory", "9.0.0" },
        { "Microsoft.Extensions.Caching.Abstractions", "9.0.0" },
        
        // Entity Framework Core
        { "Microsoft.EntityFrameworkCore", "9.0.0" },
        { "Microsoft.EntityFrameworkCore.SqlServer", "9.0.0" },
        { "Microsoft.EntityFrameworkCore.Sqlite", "9.0.0" },
        { "Microsoft.EntityFrameworkCore.InMemory", "9.0.0" },
        { "Microsoft.EntityFrameworkCore.Design", "9.0.0" },
        { "Microsoft.EntityFrameworkCore.Relational", "9.0.0" },
        
        // Data access
        { "Dapper", "2.1.35" },
        
        // Mapping
        { "AutoMapper", "13.0.1" },
        
        // Validation
        { "FluentValidation", "11.11.0" },
        
        // Logging
        { "Serilog", "4.2.0" },
        { "Serilog.Extensions.Logging", "8.0.0" },
        { "Serilog.Sinks.Console", "6.0.0" },
        { "Serilog.Sinks.File", "6.0.0" },
        
        // Resilience
        { "Polly", "8.5.0" },
        
        // Roslyn / Code Analysis
        { "Microsoft.CodeAnalysis.CSharp", "4.11.0" },
        { "Microsoft.CodeAnalysis.Common", "4.11.0" },
        
        // Testing
        { "xunit", "2.9.3" },
        { "xunit.runner.visualstudio", "3.1.4" },
        { "Moq", "4.20.72" },
        { "FluentAssertions", "6.12.2" },
        { "NSubstitute", "5.3.0" },
        { "Microsoft.NET.Test.Sdk", "17.14.1" },
        
        // HTTP
        { "RestSharp", "112.1.0" },
        { "Flurl.Http", "4.0.2" },
        
        // Serialization
        { "MessagePack", "3.0.214" },
        { "protobuf-net", "3.2.45" },
    };

    /// <summary>
    /// Gets the known version for a package, or null if not found.
    /// </summary>
    public static string? GetVersion(string packageName)
    {
        return Versions.TryGetValue(packageName, out var version) ? version : null;
    }

    /// <summary>
    /// Gets the known version for a package, with a default fallback for unknown packages.
    /// Logs a warning for unknown packages and uses a more conservative fallback strategy.
    /// </summary>
    public static string GetVersionWithFallback(string packageName, string fallbackVersion = "1.0.0")
    {
        if (Versions.TryGetValue(packageName, out var version))
        {
            return version;
        }
        
        // Try to infer version for Microsoft packages
        if (packageName.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase) ||
            packageName.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"   ℹ️  Package '{packageName}' not in known list, using 9.0.0 for .NET 9 compatibility");
            return "9.0.0";
        }
        
        // Log warning for unknown packages - this helps identify packages that should be added to the known list
        Console.WriteLine($"   ⚠️  Unknown package '{packageName}', using fallback version {fallbackVersion}. Consider adding this package to KnownPackageVersions.");
        return fallbackVersion;
    }
}
