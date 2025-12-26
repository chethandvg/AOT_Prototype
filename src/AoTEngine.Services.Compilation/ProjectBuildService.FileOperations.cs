using System.Text;
using System.Text.RegularExpressions;
using AoTEngine.Models;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing file operations and code saving methods.
/// </summary>
public partial class ProjectBuildService
{
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
}
