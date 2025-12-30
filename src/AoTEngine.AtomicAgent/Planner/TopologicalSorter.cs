using AoTEngine.AtomicAgent.Models;

namespace AoTEngine.AtomicAgent.Planner;

/// <summary>
/// Implements Kahn's Algorithm for topological sorting of atoms.
/// Section 5.3 of the architectural blueprint.
/// </summary>
public class TopologicalSorter
{
    /// <summary>
    /// Performs topological sort on atoms using Kahn's Algorithm.
    /// Returns a linearized execution order or throws if circular dependencies detected.
    /// </summary>
    public static List<Atom> Sort(List<Atom> atoms)
    {
        // Step 1: Calculate in-degrees
        var inDegree = new Dictionary<string, int>();
        var atomMap = atoms.ToDictionary(a => a.Id);

        foreach (var atom in atoms)
        {
            if (!inDegree.ContainsKey(atom.Id))
            {
                inDegree[atom.Id] = 0;
            }

            foreach (var dep in atom.Dependencies)
            {
                if (!inDegree.ContainsKey(dep))
                {
                    inDegree[dep] = 0;
                }
            }
        }

        // Count incoming edges
        foreach (var atom in atoms)
        {
            foreach (var dep in atom.Dependencies)
            {
                if (atomMap.ContainsKey(dep))
                {
                    inDegree[atom.Id]++;
                }
            }
        }

        // Step 2: Initialize queue with nodes having in-degree 0
        var queue = new Queue<Atom>();
        foreach (var atom in atoms)
        {
            if (inDegree[atom.Id] == 0)
            {
                queue.Enqueue(atom);
            }
        }

        // Step 3: Process queue
        var sortedList = new List<Atom>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sortedList.Add(current);

            // Find all atoms that depend on current
            var dependents = atoms.Where(a => a.Dependencies.Contains(current.Id));

            foreach (var dependent in dependents)
            {
                inDegree[dependent.Id]--;
                if (inDegree[dependent.Id] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        // Step 4: Cycle detection
        if (sortedList.Count < atoms.Count)
        {
            var missing = atoms.Where(a => !sortedList.Contains(a)).ToList();
            var cycle = DetectCycle(missing, atomMap);
            throw new CircularDependencyException(
                $"Circular dependency detected in atoms: {string.Join(" -> ", cycle)}");
        }

        return sortedList;
    }

    /// <summary>
    /// Detects and returns a cycle in the dependency graph.
    /// </summary>
    private static List<string> DetectCycle(List<Atom> atoms, Dictionary<string, Atom> atomMap)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var cycle = new List<string>();

        foreach (var atom in atoms)
        {
            if (DFS(atom.Id, atomMap, visited, recursionStack, cycle))
            {
                return cycle;
            }
        }

        return cycle;
    }

    private static bool DFS(
        string atomId, 
        Dictionary<string, Atom> atomMap,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> cycle)
    {
        if (recursionStack.Contains(atomId))
        {
            cycle.Add(atomId);
            return true;
        }

        if (visited.Contains(atomId))
        {
            return false;
        }

        visited.Add(atomId);
        recursionStack.Add(atomId);

        if (atomMap.TryGetValue(atomId, out var atom))
        {
            foreach (var dep in atom.Dependencies)
            {
                if (DFS(dep, atomMap, visited, recursionStack, cycle))
                {
                    cycle.Insert(0, atomId);
                    return true;
                }
            }
        }

        recursionStack.Remove(atomId);
        return false;
    }
}

public class CircularDependencyException : Exception
{
    public CircularDependencyException(string message) : base(message) { }
}
