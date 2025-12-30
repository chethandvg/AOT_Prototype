using AoTEngine.AtomicAgent.Blackboard;
using AoTEngine.AtomicAgent.Context;
using AoTEngine.AtomicAgent.Models;
using AoTEngine.AtomicAgent.Roslyn;
using AoTEngine.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AoTEngine.AtomicAgent.Execution;

/// <summary>
/// The Atomic Worker Agent executes individual atoms by generating code via LLM.
/// Section 8 of the architectural blueprint.
/// </summary>
public class AtomicWorkerAgent
{
    private readonly OpenAIService _openAIService;
    private readonly ContextEngine _contextEngine;
    private readonly RoslynFeedbackLoop _roslynLoop;
    private readonly BlackboardService _blackboard;
    private readonly ILogger<AtomicWorkerAgent> _logger;
    private readonly int _maxRetries;

    public AtomicWorkerAgent(
        OpenAIService openAIService,
        ContextEngine contextEngine,
        RoslynFeedbackLoop roslynLoop,
        BlackboardService blackboard,
        ILogger<AtomicWorkerAgent> logger,
        int maxRetries = 3)
    {
        _openAIService = openAIService;
        _contextEngine = contextEngine;
        _roslynLoop = roslynLoop;
        _blackboard = blackboard;
        _logger = logger;
        _maxRetries = maxRetries;
    }

    /// <summary>
    /// Executes an atom: generates code, validates, and updates the blackboard.
    /// </summary>
    public async Task<bool> ExecuteAtomAsync(Atom atom)
    {
        _logger.LogInformation("Executing atom {AtomId}: {Name} ({Type})", 
            atom.Id, atom.Name, atom.Type);

        _blackboard.UpdateAtomStatus(atom.Id, AtomStatus.InProgress);

        for (int attempt = 0; attempt < _maxRetries; attempt++)
        {
            try
            {
                // Build context
                var context = _contextEngine.BuildContext(atom);

                // Generate code
                string code;
                if (attempt == 0)
                {
                    code = await GenerateCodeAsync(context, atom);
                }
                else
                {
                    // Re-generate with error feedback
                    var errors = string.Join("\n", atom.CompileErrors);
                    code = await RegenerateCodeWithErrorsAsync(context, atom, errors);
                }

                // Extract code from markdown
                code = ExtractCodeFromMarkdown(code);

                // Update atom with generated code
                atom.GeneratedCode = code;
                atom.RetryCount = attempt;
                _blackboard.UpsertAtom(atom);

                // Move to review status
                _blackboard.UpdateAtomStatus(atom.Id, AtomStatus.Review);

                // Validate with Roslyn
                var compilationResult = _roslynLoop.CompileInMemory(atom);

                if (compilationResult.Success)
                {
                    _logger.LogInformation("✓ Atom {AtomId} compiled successfully", atom.Id);
                    
                    // Cache the code
                    _contextEngine.CacheCode(atom.Id, code);
                    
                    // Mark as completed
                    _blackboard.UpdateAtomStatus(atom.Id, AtomStatus.Completed);
                    return true;
                }
                else
                {
                    _logger.LogWarning("✗ Atom {AtomId} compilation failed (attempt {Attempt}/{Max})", 
                        atom.Id, attempt + 1, _maxRetries);
                    
                    atom.CompileErrors = compilationResult.Errors;
                    _blackboard.UpsertAtom(atom);

                    if (attempt == _maxRetries - 1)
                    {
                        _logger.LogError("Atom {AtomId} failed after {Attempts} attempts", 
                            atom.Id, _maxRetries);
                        _blackboard.UpdateAtomStatus(atom.Id, AtomStatus.Failed);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception executing atom {AtomId}", atom.Id);
                atom.CompileErrors.Add($"Exception: {ex.Message}");
                _blackboard.UpsertAtom(atom);

                if (attempt == _maxRetries - 1)
                {
                    _blackboard.UpdateAtomStatus(atom.Id, AtomStatus.Failed);
                    return false;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Generates code for the first time.
    /// </summary>
    private async Task<string> GenerateCodeAsync(string context, Atom atom)
    {
        var prompt = $@"{context}

Generate the complete C# code for {atom.Name}.
Output ONLY the C# code wrapped in ```csharp ... ``` markers.
Do NOT include explanations or comments outside the code block.";

        _logger.LogDebug("Generating code for {AtomId} with {ContextLength} chars of context", 
            atom.Id, context.Length);

        // Use the existing OpenAI service
        var taskNode = new AoTEngine.Models.TaskNode
        {
            Id = atom.Id,
            Description = $"Generate {atom.Type} {atom.Name} in layer {atom.Layer}",
            Context = prompt
        };

        var code = await _openAIService.GenerateCodeAsync(taskNode, new Dictionary<string, AoTEngine.Models.TaskNode>());
        return code;
    }

    /// <summary>
    /// Re-generates code with compilation error feedback.
    /// </summary>
    private async Task<string> RegenerateCodeWithErrorsAsync(string context, Atom atom, string errors)
    {
        var prompt = $@"{context}

The previous code for {atom.Name} failed to compile with the following errors:

{errors}

Please fix these errors and regenerate the complete C# code.
Output ONLY the corrected C# code wrapped in ```csharp ... ``` markers.";

        _logger.LogDebug("Re-generating code for {AtomId} with error feedback", atom.Id);

        var taskNode = new AoTEngine.Models.TaskNode
        {
            Id = atom.Id,
            Description = $"Fix {atom.Name}",
            Context = prompt
        };

        var validationResult = new AoTEngine.Models.ValidationResult 
        { 
            IsValid = false,
            Errors = atom.CompileErrors 
        };

        var code = await _openAIService.RegenerateCodeWithErrorsAsync(
            taskNode, 
            validationResult,
            null);

        return code;
    }

    /// <summary>
    /// Extracts C# code from markdown code blocks.
    /// </summary>
    private string ExtractCodeFromMarkdown(string response)
    {
        // Match ```csharp ... ``` or ``` ... ```
        const string pattern = @"```(?:csharp)?\s*(.*?)\s*```";
        var match = Regex.Match(response, pattern, RegexOptions.Singleline);

        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // If no markdown block, return as-is
        return response.Trim();
    }
}
