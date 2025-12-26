using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AoTEngine.Models;

namespace AoTEngine.Services;

/// <summary>
/// Service for building .NET projects at specified locations.
/// This is the main partial class containing core fields and constructors.
/// </summary>
/// <remarks>
/// This class is split into multiple partial class files for maintainability:
/// - ProjectBuildService.cs (this file): Core fields, constructors, and CreateProjectFromTasksAsync
/// - ProjectBuildService.PackageManagement.cs: Package extraction and management methods
/// - ProjectBuildService.FileOperations.cs: File saving and entry point creation methods
/// - ProjectBuildService.BuildValidation.cs: Build and restore methods
/// </remarks>
public partial class ProjectBuildService
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
