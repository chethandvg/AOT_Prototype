using System.Diagnostics;
using System.Text;
using AoTEngine.Models;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing build and validation methods.
/// </summary>
public partial class ProjectBuildService
{
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
            Console.WriteLine($"\n📦 Creating project '{projectName}' at: {outputDirectory}");
            
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
            
            // Create new console project
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
            
            Console.WriteLine("   ✓ Project created");
            
            // Write generated code to Program.cs
            var programPath = Path.Combine(projectPath, "Program.cs");
            await File.WriteAllTextAsync(programPath, generatedCode);
            Console.WriteLine("   ✓ Generated code written to Program.cs");
            
            result.ProjectPath = projectPath;
            result.ProgramFilePath = programPath;
            
            // Build the project to validate
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
                    Console.WriteLine($"   ⚠️  {warnings.Count} warning(s) found");
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Build exception: {ex.Message}";
            Console.WriteLine($"\n✗ Build error: {ex.Message}");
        }
        
        return result;
    }
}
