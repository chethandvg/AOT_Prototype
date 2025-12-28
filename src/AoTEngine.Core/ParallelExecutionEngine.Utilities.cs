using AoTEngine.Models;
using AoTEngine.Services;

namespace AoTEngine.Core;

/// <summary>
/// Partial class containing code combination and utility methods.
/// </summary>
public partial class ParallelExecutionEngine
{
    /// <summary>
    /// Combines all generated code from tasks into a single code snippet.
    /// </summary>
    private string CombineGeneratedCode(List<TaskNode> tasks)
    {
        var usings = new HashSet<string>();
        var namespaces = new Dictionary<string, List<string>>();
        
        foreach (var task in tasks.Where(t => !string.IsNullOrWhiteSpace(t.GeneratedCode)))
        {
            var lines = task.GeneratedCode.Split('\n');
            var currentNamespace = "Global";
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Collect using statements
                if (trimmedLine.StartsWith("using ") && trimmedLine.EndsWith(";"))
                {
                    usings.Add(trimmedLine);
                    continue;
                }
                
                // Track namespace
                if (trimmedLine.StartsWith("namespace "))
                {
                    currentNamespace = trimmedLine.Replace("namespace ", "")
                        .Replace(";", "")
                        .Replace("{", "")
                        .Trim();
                    
                    if (!namespaces.ContainsKey(currentNamespace))
                    {
                        namespaces[currentNamespace] = new List<string>();
                    }
                    continue;
                }
                
                // Skip closing braces for namespaces
                if (trimmedLine == "}" && currentNamespace != "Global")
                {
                    continue;
                }
                
                // Add content to current namespace
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    if (!namespaces.ContainsKey(currentNamespace))
                    {
                        namespaces[currentNamespace] = new List<string>();
                    }
                    namespaces[currentNamespace].Add(line);
                }
            }
        }
        
        // Build combined code
        var combined = new System.Text.StringBuilder();
        
        // Add all using statements
        foreach (var usingStatement in usings.OrderBy(u => u))
        {
            combined.AppendLine(usingStatement);
        }
        
        combined.AppendLine();
        
        // Add each namespace with its content
        foreach (var ns in namespaces.OrderBy(kvp => kvp.Key))
        {
            if (ns.Key == "Global")
            {
                foreach (var line in ns.Value)
                {
                    combined.AppendLine(line);
                }
            }
            else
            {
                combined.AppendLine($"namespace {ns.Key}");
                combined.AppendLine("{");
                foreach (var line in ns.Value)
                {
                    combined.AppendLine(line);
                }
                combined.AppendLine("}");
            }
        }
        
        return combined.ToString();
    }

    /// <summary>
    /// Builds a topologically sorted list of tasks for visualization.
    /// </summary>
    public List<TaskNode> TopologicalSort(List<TaskNode> tasks)
    {
        var sorted = new List<TaskNode>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        var taskDict = tasks.ToDictionary(t => t.Id, t => t);

        void Visit(TaskNode task)
        {
            if (visited.Contains(task.Id))
                return;

            if (visiting.Contains(task.Id))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected involving task {task.Id}");
            }

            visiting.Add(task.Id);

            foreach (var depId in task.Dependencies.Where(depId => taskDict.ContainsKey(depId)))
            {
                Visit(taskDict[depId]);
            }

            visiting.Remove(task.Id);
            visited.Add(task.Id);
            sorted.Add(task);
        }

        foreach (var task in tasks)
        {
            Visit(task);
        }

        return sorted;
    }
}
