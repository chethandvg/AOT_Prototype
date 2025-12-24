using System.Diagnostics;
using System.Text;
using AoTEngine.Models;

namespace AoTEngine.Services;

/// <summary>
/// Service for building .NET projects at specified locations.
/// </summary>
public class ProjectBuildService
{
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
}
