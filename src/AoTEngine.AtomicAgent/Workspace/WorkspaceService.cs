using Microsoft.Extensions.Logging;

namespace AoTEngine.AtomicAgent.Workspace;

/// <summary>
/// Manages the workspace directory with sandboxing to prevent accidental system file modification.
/// Implements Section 3.3 of the architectural blueprint.
/// </summary>
public class WorkspaceService
{
    private readonly string _rootPath;
    private readonly bool _enableSandboxing;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(string rootPath, bool enableSandboxing, ILogger<WorkspaceService> logger)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _enableSandboxing = enableSandboxing;
        _logger = logger;

        // Create workspace directory if it doesn't exist
        Directory.CreateDirectory(_rootPath);
        _logger.LogInformation("Workspace initialized at: {RootPath}", _rootPath);
    }

    public string RootPath => _rootPath;

    /// <summary>
    /// Validates that a path is within the workspace boundary.
    /// </summary>
    public bool IsPathSafe(string path)
    {
        if (!_enableSandboxing)
            return true;

        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a safe path within the workspace.
    /// </summary>
    public string GetSafePath(string relativePath)
    {
        var fullPath = Path.Combine(_rootPath, relativePath);
        fullPath = Path.GetFullPath(fullPath);

        if (_enableSandboxing && !IsPathSafe(fullPath))
        {
            throw new WorkspaceSecurityException($"Path '{relativePath}' is outside the workspace boundary: {_rootPath}");
        }

        return fullPath;
    }

    /// <summary>
    /// Creates a directory within the workspace.
    /// </summary>
    public string CreateDirectory(string relativePath)
    {
        var safePath = GetSafePath(relativePath);
        Directory.CreateDirectory(safePath);
        _logger.LogDebug("Created directory: {Path}", safePath);
        return safePath;
    }

    /// <summary>
    /// Writes content to a file within the workspace.
    /// </summary>
    public async Task WriteFileAsync(string relativePath, string content)
    {
        var safePath = GetSafePath(relativePath);
        var directory = Path.GetDirectoryName(safePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(safePath, content);
        _logger.LogInformation("Wrote file: {Path} ({Bytes} bytes)", safePath, content.Length);
    }

    /// <summary>
    /// Reads content from a file within the workspace.
    /// </summary>
    public async Task<string> ReadFileAsync(string relativePath)
    {
        var safePath = GetSafePath(relativePath);
        
        if (!File.Exists(safePath))
        {
            throw new FileNotFoundException($"File not found: {safePath}");
        }

        var content = await File.ReadAllTextAsync(safePath);
        _logger.LogDebug("Read file: {Path} ({Bytes} bytes)", safePath, content.Length);
        return content;
    }

    /// <summary>
    /// Checks if a file exists within the workspace.
    /// </summary>
    public bool FileExists(string relativePath)
    {
        var safePath = GetSafePath(relativePath);
        return File.Exists(safePath);
    }

    /// <summary>
    /// Initializes a .NET solution using the dotnet CLI.
    /// </summary>
    public async Task<bool> InitializeSolutionAsync(string solutionName)
    {
        try
        {
            // Validate solution name to prevent command injection
            if (!IsValidProjectName(solutionName))
            {
                _logger.LogError("Invalid solution name: {SolutionName}. Must contain only alphanumeric characters, underscores, hyphens, and periods.", solutionName);
                return false;
            }

            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"new sln -n {solutionName}",
                WorkingDirectory = _rootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start dotnet process");
                return false;
            }

            await process.WaitForExitAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Solution created: {SolutionName}", solutionName);
                return true;
            }
            else
            {
                _logger.LogError("Failed to create solution: {Error}", error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while initializing solution");
            return false;
        }
    }

    /// <summary>
    /// Creates a class library project using the dotnet CLI.
    /// </summary>
    public async Task<bool> CreateClassLibraryAsync(string projectName, string relativePath)
    {
        try
        {
            // Validate project name to prevent command injection
            if (!IsValidProjectName(projectName))
            {
                _logger.LogError("Invalid project name: {ProjectName}. Must contain only alphanumeric characters, underscores, hyphens, and periods.", projectName);
                return false;
            }

            var projectDir = GetSafePath(relativePath);
            Directory.CreateDirectory(projectDir);

            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"new classlib -n {projectName} -f net9.0",
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start dotnet process");
                return false;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Class library created: {ProjectName} at {Path}", projectName, projectDir);
                return true;
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogError("Failed to create class library: {Error}", error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while creating class library");
            return false;
        }
    }

    /// <summary>
    /// Validates that a project or solution name contains only safe characters.
    /// Prevents command injection by restricting to alphanumeric, underscores, hyphens, and periods.
    /// </summary>
    private bool IsValidProjectName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Allow only alphanumeric characters, underscores, hyphens, and periods
        return System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9_\-\.]+$");
    }
}

public class WorkspaceSecurityException : Exception
{
    public WorkspaceSecurityException(string message) : base(message) { }
}
