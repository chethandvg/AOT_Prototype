using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AoTEngine.Models;

namespace AoTEngine.Services;

/// <summary>
/// Service for building .NET projects at specified locations.
/// </summary>
public class ProjectBuildService
{
    private readonly OpenAIService? _openAIService;

    /// <summary>
    /// Creates a new ProjectBuildService without OpenAI integration.
    /// Package versions will use fallback defaults.
    /// </summary>
    public ProjectBuildService()
    {
        _openAIService = null;
    }

    /// <summary>
    /// Creates a new ProjectBuildService with OpenAI integration for dynamic package version resolution.
    /// </summary>
    /// <param name="openAIService">OpenAI service for querying latest package versions</param>
    public ProjectBuildService(OpenAIService openAIService)
    {
        _openAIService = openAIService;
    }

    /// <summary>
    /// Creates a new .NET project from a list of tasks, saving each task's code to separate files
    /// and adding required package references dynamically.
    /// Uses OpenAI to get latest stable package versions compatible with .NET 9.
    /// </summary>
    /// <param name="outputDirectory">Directory where the project should be created</param>
    /// <param name="projectName">Name of the project</param>
    /// <param name="tasks">List of completed tasks with generated code</param>
    /// <returns>Build result with project creation and validation status</returns>
    public async Task<ProjectBuildResult> CreateProjectFromTasksAsync(
        string outputDirectory,
        string projectName,
        List<TaskNode> tasks)
    {
        var result = new ProjectBuildResult();

        try
        {
            Console.WriteLine($"\n📦 Creating project '{projectName}' from {tasks.Count} task(s) at: {outputDirectory}");

            // Create output directory if it doesn't exist
            Directory.CreateDirectory(outputDirectory);

            var projectPath = Path.Combine(outputDirectory, projectName);

            // Clean up old project if it exists
            if (Directory.Exists(projectPath))
            {
                Console.WriteLine($"   🗑️ Cleaning up existing project directory...");
                Directory.Delete(projectPath, true);
            }

            Directory.CreateDirectory(projectPath);

            // Step 1: Extract all required package names from tasks before creating the project
            Console.WriteLine("   📋 Extracting required packages from generated code...");
            var packageNames = ExtractRequiredPackageNamesFromTasks(tasks);
            
            if (packageNames.Any())
            {
                Console.WriteLine($"   📦 Found {packageNames.Count} required package(s): {string.Join(", ", packageNames)}");
            }

            // Step 2: Get package versions from OpenAI (if available) or use fallbacks
            Dictionary<string, string> allPackages;
            if (packageNames.Any())
            {
                if (_openAIService != null)
                {
                    Console.WriteLine("   🤖 Querying OpenAI for latest stable .NET 9 compatible package versions...");
                    allPackages = await _openAIService.GetPackageVersionsAsync(packageNames);
                    Console.WriteLine($"   ✓ Retrieved versions for {allPackages.Count} package(s)");
                }
                else
                {
                    Console.WriteLine("   ℹ️ Using fallback package versions (OpenAI not configured)");
                    allPackages = GetFallbackPackageVersions(packageNames);
                }
            }
            else
            {
                allPackages = new Dictionary<string, string>();
            }

            // Step 3: Create new console project
            Console.WriteLine("   🔧 Creating .NET console project...");
            var createProcessInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"new console -n {projectName} -o \"{projectPath}\" --force",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var createProcess = new Process { StartInfo = createProcessInfo })
            {
                createProcess.Start();
                // We don't need the output, but we need to read it to avoid deadlock
                _ = await createProcess.StandardOutput.ReadToEndAsync();
                var error = await createProcess.StandardError.ReadToEndAsync();
                await createProcess.WaitForExitAsync();

                if (createProcess.ExitCode != 0)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to create project: {error}";
                    return result;
                }
            }

            Console.WriteLine("   ✓ Project created");

            // Step 4: Add package references to .csproj file
            if (allPackages.Any())
            {
                Console.WriteLine("   📦 Adding package references to project...");
                var csprojPath = Path.Combine(projectPath, $"{projectName}.csproj");
                await AddPackageReferencesToCsprojAsync(csprojPath, allPackages);
                Console.WriteLine($"   ✓ Added {allPackages.Count} package reference(s)");
            }

            // Step 5: Save each task's code to separate files
            Console.WriteLine("   📁 Saving generated code to separate files...");
            var generatedFiles = await SaveTaskCodeToFilesAsync(projectPath, tasks);
            Console.WriteLine($"   ✓ Created {generatedFiles.Count} code file(s)");

            // Step 6: Create a minimal Program.cs entry point if not already generated
            await CreateEntryPointIfNeededAsync(projectPath, tasks);

            result.ProjectPath = projectPath;
            result.ProgramFilePath = Path.Combine(projectPath, "Program.cs");
            result.GeneratedFiles = generatedFiles;

            // Step 7: Restore packages
            Console.WriteLine("\n📥 Restoring packages...");
            var restoreResult = await RestorePackagesAsync(projectPath);
            if (!restoreResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = $"Package restore failed: {restoreResult.ErrorMessage}";
                result.Errors = restoreResult.Errors;
                return result;
            }
            Console.WriteLine("   ✓ Packages restored");

            // Step 8: Build the project to validate
            Console.WriteLine("\n🔨 Building project to validate code...");
            var buildResult = await BuildProjectAsync(projectPath);

            result.Success = buildResult.Success;
            result.BuildOutput = buildResult.BuildOutput;
            result.ErrorMessage = buildResult.ErrorMessage;
            result.OutputAssemblyPath = buildResult.OutputAssemblyPath;
            result.Errors = buildResult.Errors;
            result.Warnings = buildResult.Warnings;

            if (result.Success)
            {
                Console.WriteLine("✓ Project built successfully!");
            }
            else
            {
                Console.WriteLine($"✗ Project build failed!");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Project creation exception: {ex.Message}";
            Console.WriteLine($"\n✗ Project creation error: {ex.Message}");
        }

        return result;
    }

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

    /// <summary>
    /// Saves each task's code to a separate file based on the types defined in the code.
    /// </summary>
    private async Task<List<string>> SaveTaskCodeToFilesAsync(string projectPath, List<TaskNode> tasks)
    {
        var generatedFiles = new List<string>();

        foreach (var task in tasks.Where(t => !string.IsNullOrWhiteSpace(t.GeneratedCode)))
        {
            // Generate filename based on task ID and expected types
            var filename = GenerateFilenameForTask(task);
            var filePath = Path.Combine(projectPath, filename);

            // Handle namespace-based subdirectories
            if (!string.IsNullOrEmpty(task.Namespace) && task.Namespace.Contains('.'))
            {
                var namespaceDir = task.Namespace.Replace('.', Path.DirectorySeparatorChar);
                var dirPath = Path.Combine(projectPath, namespaceDir);
                Directory.CreateDirectory(dirPath);
                filePath = Path.Combine(dirPath, filename);
            }

            await File.WriteAllTextAsync(filePath, task.GeneratedCode);
            generatedFiles.Add(filePath);
            Console.WriteLine($"      📄 {Path.GetFileName(filePath)}");
        }

        return generatedFiles;
    }

    /// <summary>
    /// Generates an appropriate filename for a task's code based on its content.
    /// </summary>
    private string GenerateFilenameForTask(TaskNode task)
    {
        // Try to extract the main type name from the code
        var typeNameMatch = Regex.Match(
            task.GeneratedCode,
            @"(?:public|internal|private|protected)?\s*(?:static\s+)?(?:partial\s+)?(?:class|interface|struct|record|enum)\s+([A-Z][a-zA-Z0-9_]+)");

        if (typeNameMatch.Success)
        {
            return $"{typeNameMatch.Groups[1].Value}.cs";
        }

        // Fallback to using expected types
        if (task.ExpectedTypes.Any())
        {
            return $"{task.ExpectedTypes.First()}.cs";
        }

        // Fallback to task ID
        return $"{SanitizeFilename(task.Id)}.cs";
    }

    /// <summary>
    /// Sanitizes a string to be used as a filename.
    /// </summary>
    private string SanitizeFilename(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", input.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Creates a minimal Program.cs entry point if the generated code doesn't include one.
    /// Uses more robust detection to avoid false positives from comments or strings.
    /// </summary>
    private async Task CreateEntryPointIfNeededAsync(string projectPath, List<TaskNode> tasks)
    {
        var programPath = Path.Combine(projectPath, "Program.cs");

        // Check if any task already generated a Main method or top-level statements
        // More robust detection that looks for actual entry point patterns
        var hasEntryPoint = tasks.Any(t => 
            !string.IsNullOrWhiteSpace(t.GeneratedCode) &&
            (t.GeneratedCode.Contains("static void Main(") ||
             t.GeneratedCode.Contains("static async Task Main(") ||
             t.GeneratedCode.Contains("static Task Main(") ||
             t.GeneratedCode.Contains("static int Main(") ||
             // Look for top-level statements pattern (code outside of type declarations at start)
             HasTopLevelStatements(t.GeneratedCode)));

        if (!hasEntryPoint)
        {
            // Create a minimal entry point that references the generated types
            var entryPoint = GenerateMinimalEntryPoint(tasks);
            await File.WriteAllTextAsync(programPath, entryPoint);
            Console.WriteLine($"      📄 Program.cs (entry point)");
        }
    }

    /// <summary>
    /// Detects if code contains top-level statements (code outside of namespace/class declarations).
    /// </summary>
    private bool HasTopLevelStatements(string code)
    {
        // Remove single-line and multi-line comments to avoid false positives
        var codeWithoutComments = Regex.Replace(code, @"//.*$", "", RegexOptions.Multiline);
        codeWithoutComments = Regex.Replace(codeWithoutComments, @"/\*.*?\*/", "", RegexOptions.Singleline);
        
        // Remove string literals to avoid false positives
        codeWithoutComments = Regex.Replace(codeWithoutComments, @"""[^""]*""", "\"\"");
        codeWithoutComments = Regex.Replace(codeWithoutComments, @"@""[^""]*""", "\"\"");
        
        // Check for common top-level statement patterns after using statements
        // These patterns indicate executable code at the top level
        var lines = codeWithoutComments.Split('\n');
        bool passedUsings = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            // Skip using statements
            if (trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
            {
                passedUsings = true;
                continue;
            }
            
            // Skip namespace and class declarations
            if (trimmed.StartsWith("namespace ") || 
                trimmed.StartsWith("public ") || 
                trimmed.StartsWith("internal ") ||
                trimmed.StartsWith("class ") ||
                trimmed.StartsWith("interface ") ||
                trimmed.StartsWith("struct ") ||
                trimmed.StartsWith("record ") ||
                trimmed.StartsWith("enum ") ||
                trimmed.StartsWith("[") ||  // Attributes
                trimmed == "{" || trimmed == "}")
            {
                continue;
            }
            
            // If we've passed usings and see executable code, it's top-level
            if (passedUsings && 
                (trimmed.StartsWith("var ") ||
                 trimmed.StartsWith("await ") ||
                 trimmed.StartsWith("Console.") ||
                 trimmed.Contains("(") && trimmed.EndsWith(";")))  // Method call
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Generates a minimal entry point that demonstrates the generated types.
    /// </summary>
    private string GenerateMinimalEntryPoint(List<TaskNode> tasks)
    {
        var sb = new StringBuilder();
        var namespaces = new HashSet<string>();

        // Collect namespaces from tasks - use explicit Where filter
        foreach (var task in tasks.Where(t => !string.IsNullOrEmpty(t.Namespace)))
        {
            namespaces.Add(task.Namespace);
        }

        // Add using statements
        sb.AppendLine("// Auto-generated entry point");
        sb.AppendLine("using System;");
        foreach (var ns in namespaces.OrderBy(n => n))
        {
            sb.AppendLine($"using {ns};");
        }
        sb.AppendLine();

        // Generate a simple entry point
        sb.AppendLine("// Main entry point");
        sb.AppendLine("Console.WriteLine(\"Generated application is running.\");");
        sb.AppendLine();
        
        // Add comments about available types
        sb.AppendLine("// Available types from generated code:");
        foreach (var task in tasks.Where(t => t.ExpectedTypes.Any()))
        {
            foreach (var type in task.ExpectedTypes)
            {
                sb.AppendLine($"// - {type}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Restores NuGet packages for the project.
    /// </summary>
    private async Task<ProjectBuildResult> RestorePackagesAsync(string projectPath)
    {
        var result = new ProjectBuildResult();

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"restore \"{projectPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var output = new StringBuilder();
        var errors = new List<string>();

        using var process = new Process { StartInfo = processStartInfo };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errors.Add(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        result.Success = process.ExitCode == 0;
        result.BuildOutput = output.ToString();
        result.Errors = errors;
        
        if (!result.Success)
        {
            result.ErrorMessage = $"Restore failed with exit code {process.ExitCode}";
        }

        return result;
    }

    /// <summary>
    /// Creates a new .NET console project with generated code and validates it by building.
    /// </summary>
    /// <param name="outputDirectory">Directory where the project should be created</param>
    /// <param name="projectName">Name of the project</param>
    /// <param name="generatedCode">Generated code to include in Program.cs</param>
    /// <returns>Build result with project creation and validation status</returns>
    public async Task<ProjectBuildResult> CreateAndValidateProjectAsync(
        string outputDirectory, 
        string projectName, 
        string generatedCode)
    {
        var result = new ProjectBuildResult();
        
        try
        {
            Console.WriteLine($"\n?? Creating project '{projectName}' at: {outputDirectory}");
            
            // Create output directory if it doesn't exist
            Directory.CreateDirectory(outputDirectory);
            
            var projectPath = Path.Combine(outputDirectory, projectName);
            
            // Clean up old project if it exists
            if (Directory.Exists(projectPath))
            {
                Console.WriteLine($"   ?? Cleaning up existing project directory...");
                Directory.Delete(projectPath, true);
            }
            
            Directory.CreateDirectory(projectPath);
            
            // Create new console project
            Console.WriteLine("   ??  Creating .NET console project...");
            var createProcessInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"new console -n {projectName} -o \"{projectPath}\" --force",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using (var createProcess = new Process { StartInfo = createProcessInfo })
            {
                createProcess.Start();
                var output = await createProcess.StandardOutput.ReadToEndAsync();
                var error = await createProcess.StandardError.ReadToEndAsync();
                await createProcess.WaitForExitAsync();
                
                if (createProcess.ExitCode != 0)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to create project: {error}";
                    return result;
                }
            }
            
            Console.WriteLine("   ? Project created");
            
            // Write generated code to Program.cs
            var programPath = Path.Combine(projectPath, "Program.cs");
            await File.WriteAllTextAsync(programPath, generatedCode);
            Console.WriteLine("   ? Generated code written to Program.cs");
            
            result.ProjectPath = projectPath;
            result.ProgramFilePath = programPath;
            
            // Build the project to validate
            Console.WriteLine("\n?? Building project to validate code...");
            var buildResult = await BuildProjectAsync(projectPath);
            
            result.Success = buildResult.Success;
            result.BuildOutput = buildResult.BuildOutput;
            result.ErrorMessage = buildResult.ErrorMessage;
            result.OutputAssemblyPath = buildResult.OutputAssemblyPath;
            result.Errors = buildResult.Errors;
            result.Warnings = buildResult.Warnings;
            
            if (result.Success)
            {
                Console.WriteLine("? Project built successfully!");
            }
            else
            {
                Console.WriteLine($"? Project build failed!");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Project creation exception: {ex.Message}";
            Console.WriteLine($"\n? Project creation error: {ex.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Builds a .NET project at the specified location.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file or directory containing it</param>
    /// <param name="configuration">Build configuration (Debug/Release)</param>
    /// <returns>Build result with success status and output</returns>
    public async Task<ProjectBuildResult> BuildProjectAsync(string projectPath, string configuration = "Debug")
    {
        var result = new ProjectBuildResult();
        
        try
        {
            // Validate path exists
            if (!Directory.Exists(projectPath) && !File.Exists(projectPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Path not found: {projectPath}";
                return result;
            }
            
            // If it's a directory, look for .csproj file
            var targetPath = projectPath;
            if (Directory.Exists(projectPath))
            {
                var csprojFiles = Directory.GetFiles(projectPath, "*.csproj");
                if (csprojFiles.Length == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = $"No .csproj file found in directory: {projectPath}";
                    return result;
                }
                targetPath = csprojFiles[0];
            }
            
            result.ProjectPath = Path.GetDirectoryName(targetPath)!;
            
            // Execute dotnet build command
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{targetPath}\" --configuration {configuration} --verbosity quiet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            var output = new StringBuilder();
            var errors = new List<string>();
            var warnings = new List<string>();
            
            using var process = new Process { StartInfo = processStartInfo };
            
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                    
                    // Parse errors and warnings
                    if (e.Data.Contains("error CS") || e.Data.Contains("error MSB"))
                    {
                        errors.Add(e.Data.Trim());
                    }
                    else if (e.Data.Contains("warning CS") || e.Data.Contains("warning MSB"))
                    {
                        warnings.Add(e.Data.Trim());
                    }
                }
            };
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                    errors.Add(e.Data.Trim());
                }
            };
            
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await process.WaitForExitAsync();
            
            result.BuildOutput = output.ToString();
            result.Errors = errors;
            result.Warnings = warnings;
            result.Success = process.ExitCode == 0;
            
            if (!result.Success)
            {
                result.ErrorMessage = $"Build failed with exit code {process.ExitCode}";
                Console.WriteLine($"   Found {errors.Count} error(s) and {warnings.Count} warning(s)");
            }
            else
            {
                // Get the output assembly path
                var binPath = Path.Combine(Path.GetDirectoryName(targetPath)!, "bin", configuration);
                if (Directory.Exists(binPath))
                {
                    var dllFiles = Directory.GetFiles(binPath, "*.dll", SearchOption.AllDirectories);
                    if (dllFiles.Length > 0)
                    {
                        result.OutputAssemblyPath = dllFiles[0];
                    }
                }
                
                if (warnings.Count > 0)
                {
                    Console.WriteLine($"   ??  {warnings.Count} warning(s) found");
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Build exception: {ex.Message}";
            Console.WriteLine($"\n? Build error: {ex.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Converts build result to ValidationResult for use with existing validation flow.
    /// </summary>
    public ValidationResult ConvertToValidationResult(ProjectBuildResult buildResult)
    {
        var validationResult = new ValidationResult
        {
            IsValid = buildResult.Success
        };
        
        if (buildResult.Errors != null)
        {
            validationResult.Errors.AddRange(buildResult.Errors.Select(e => $"Build Error: {e}"));
        }
        
        if (buildResult.Warnings != null)
        {
            validationResult.Warnings.AddRange(buildResult.Warnings.Select(w => $"Build Warning: {w}"));
        }
        
        return validationResult;
    }
}

/// <summary>
/// Result of a project build operation.
/// </summary>
public class ProjectBuildResult
{
    /// <summary>
    /// Whether the build succeeded.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Path to the project directory.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to Program.cs file.
    /// </summary>
    public string ProgramFilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the output assembly (DLL/EXE).
    /// </summary>
    public string OutputAssemblyPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Build output messages.
    /// </summary>
    public string BuildOutput { get; set; } = string.Empty;
    
    /// <summary>
    /// List of build errors.
    /// </summary>
    public List<string> Errors { get; set; } = new List<string>();
    
    /// <summary>
    /// List of build warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new List<string>();
    
    /// <summary>
    /// Error message if build failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// List of generated code files.
    /// </summary>
    public List<string> GeneratedFiles { get; set; } = new List<string>();
}
