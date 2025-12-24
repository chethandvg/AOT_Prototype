namespace AoTEngine.Models;

/// <summary>
/// Response model containing decomposed tasks in DAG format.
/// </summary>
public class TaskDecompositionResponse
{
    /// <summary>
    /// List of atomic tasks in the DAG.
    /// </summary>
    public List<TaskNode> Tasks { get; set; } = new();

    /// <summary>
    /// Overall description of the decomposition.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
