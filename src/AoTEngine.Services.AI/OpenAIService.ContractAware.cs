using AoTEngine.Models;
using OpenAI.Chat;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing contract-aware code generation methods.
/// These methods use the Contract-First approach to prevent type mismatches and missing members.
/// Integrates with the gpt-5.1-codex Responses API to leverage response chaining for better
/// contract compliance and context awareness.
/// </summary>
/// <remarks>
/// Contract-Aware Generation with Response Chaining:
/// This approach combines frozen contract validation with response chaining to achieve:
/// - Strict adherence to predefined interface and type contracts
/// - Context continuity from dependency responses
/// - Iterative refinement with contract violation feedback
/// - Reduced type mismatch errors through explicit contract context
/// 
/// The response chaining capability is particularly valuable in contract-aware scenarios because:
/// - Contract definitions can be referenced implicitly through chained context
/// - Error corrections maintain awareness of contract requirements
/// - Dependent implementations can build upon contract-compliant dependencies
/// </remarks>
public partial class OpenAIService
{
    private ContractCatalog? _contractCatalog;
    private PromptContextBuilder? _promptContextBuilder;

    /// <summary>
    /// Sets the contract catalog for contract-aware code generation.
    /// </summary>
    /// <param name="catalog">The frozen contract catalog.</param>
    /// <param name="symbolTable">The symbol table for type tracking.</param>
    /// <param name="typeRegistry">The type registry for collision detection.</param>
    public void SetContractCatalog(
        ContractCatalog catalog,
        SymbolTable symbolTable,
        TypeRegistry typeRegistry)
    {
        _contractCatalog = catalog;
        _promptContextBuilder = new PromptContextBuilder(catalog, symbolTable, typeRegistry);
    }

    /// <summary>
    /// Generates code for a task using contract-aware context with response chaining.
    /// This method uses frozen contracts to prevent type mismatches and missing members.
    /// </summary>
    /// <param name="task">The task node containing generation requirements.</param>
    /// <param name="completedTasks">Dictionary of completed tasks that this task may depend on.</param>
    /// <returns>The generated C# code that complies with frozen contracts.</returns>
    /// <remarks>
    /// Contract-Aware Response Chaining:
    /// This method combines two powerful techniques:
    /// 
    /// 1. Frozen Contracts: Provides explicit type definitions and interface signatures that
    ///    implementations must conform to, preventing signature mismatches.
    /// 
    /// 2. Response Chaining: Links to previous dependency responses, giving the model implicit
    ///    context about how contracts were implemented in related code.
    /// 
    /// The combination ensures both structural compliance (via contracts) and contextual coherence
    /// (via chaining). When contract violations are detected, the method can iterate with the
    /// violation feedback while maintaining response chain context.
    /// 
    /// Fallback Behavior:
    /// If no contract catalog is configured, falls back to regular code generation without
    /// contract validation but still with response chaining support.
    /// </remarks>
    public async Task<string> GenerateCodeWithContractsAsync(
        TaskNode task,
        Dictionary<string, TaskNode> completedTasks)
    {
        // If no contract catalog is set, fall back to regular code generation
        if (_contractCatalog == null || _promptContextBuilder == null)
        {
            return await GenerateCodeAsync(task, completedTasks);
        }

        // Build enhanced context with contracts using StringBuilder for efficiency
        var enhancedContextBuilder = new StringBuilder();
        enhancedContextBuilder.Append(_promptContextBuilder.BuildCodeGenerationContext(task, completedTasks));

        // Determine if we can chain from a previous response
        string? previousResponseId = null;
        if (task.Dependencies.Any())
        {
            var lastDep = task.Dependencies.LastOrDefault(depId => _responseChain.ContainsKey(depId));
            if (lastDep != null)
            {
                previousResponseId = _responseChain[lastDep];
            }
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var systemPrompt = GetContractAwareSystemPrompt(attempt);
                var userPrompt = GetContractAwareUserPrompt(enhancedContextBuilder.ToString(), attempt, task);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                // Use Codex Responses API with chaining
                var (responseId, content) = await CallCodexResponsesAsync(messages, previousResponseId);
                
                // Store response ID for potential chaining
                _responseChain[task.Id] = responseId;
                
                var generatedCode = CleanGeneratedCode(content);

                // Validate against contracts
                var contractViolations = _promptContextBuilder.ValidateAgainstContracts(generatedCode, task);
                if (contractViolations.Any())
                {
                    Console.WriteLine($"   ⚠️  Contract violations detected in task {task.Id}:");
                    foreach (var violation in contractViolations)
                    {
                        Console.WriteLine($"      - {violation}");
                    }

                    // If not the last attempt, regenerate with violation feedback
                    if (attempt < MaxRetries - 1)
                    {
                        Console.WriteLine("   Regenerating with contract violation feedback...");
                        enhancedContextBuilder.AppendLine();
                        enhancedContextBuilder.AppendLine("/* CONTRACT VIOLATIONS TO FIX:");
                        foreach (var violation in contractViolations)
                        {
                            enhancedContextBuilder.AppendLine(violation);
                        }
                        enhancedContextBuilder.AppendLine("*/");
                        // Update previousResponseId to chain from this attempt
                        previousResponseId = responseId;
                        continue;
                    }
                }

                // Extract and store type contract after generation
                task.TypeContract = ExtractTypeContract(generatedCode);

                return generatedCode;
            }
            catch (HttpRequestException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"HTTP error during code generation (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        throw new InvalidOperationException("Failed to generate code after multiple attempts");
    }

    /// <summary>
    /// Gets the system prompt for contract-aware code generation.
    /// </summary>
    private string GetContractAwareSystemPrompt(int attempt)
    {
        var basePrompt = @"You are an expert C# programmer implementing code against FROZEN CONTRACTS.

CRITICAL: The contracts (interfaces, enums, abstract classes, models) have been frozen and CANNOT be changed.
Your implementation MUST conform exactly to these contracts.

CONTRACT COMPLIANCE RULES:
1. IMPLEMENT all interface methods with EXACT return types and parameter signatures
2. OVERRIDE all abstract methods with EXACT signatures
3. USE only enum members that exist in the contract (do not invent new ones)
4. DO NOT redefine any types from the frozen contracts
5. DO NOT inherit from sealed classes - use composition instead
6. USE fully qualified type names when there could be ambiguity
7. PLACE implementations in the correct namespace (services in .Services, models in .Models)
8. MATCH async/await patterns exactly (Task<T> vs T, CancellationToken placement)

OUTPUT REQUIREMENTS:
- Return ONLY valid C# code without markdown formatting
- Include all necessary using statements
- Use file-scoped namespaces
- Add XML documentation for public members
- Ensure the code compiles against the frozen contracts";

        if (attempt >= 2)
        {
            basePrompt += @"

⚠️ CRITICAL: This is the FINAL attempt. Previous attempts had contract violations.
Take MAXIMUM CARE to ensure EXACT compliance with all contracts.
Double-check every method signature, return type, and parameter type.";
        }

        return basePrompt;
    }

    /// <summary>
    /// Gets the user prompt for contract-aware code generation.
    /// </summary>
    private string GetContractAwareUserPrompt(string enhancedContext, int attempt, TaskNode task)
    {
        var prompt = $@"Generate C# code for the following task, ensuring EXACT compliance with frozen contracts:

{enhancedContext}

OUTPUT FORMAT:
1. Start with all necessary using directives
2. Use file-scoped namespace: {task.Namespace ?? "ProjectName.Feature"}
3. Implement all expected types: {string.Join(", ", task.ExpectedTypes)}
4. Follow all CONTRACT COMPLIANCE RULES from the system prompt

Return ONLY the C# code without any markdown formatting or explanations.";

        if (attempt >= 2)
        {
            prompt += @"

⚠️ FINAL ATTEMPT - Double-check all signatures before generating!";
        }

        return prompt;
    }

    /// <summary>
    /// Cleans up generated code by removing markdown formatting.
    /// </summary>
    private string CleanGeneratedCode(string code)
    {
        code = code.Trim();

        // Remove markdown code blocks
        if (code.StartsWith("```csharp"))
        {
            code = code.Substring(9);
        }
        else if (code.StartsWith("```cs"))
        {
            code = code.Substring(5);
        }
        else if (code.StartsWith("```"))
        {
            code = code.Substring(3);
        }

        if (code.EndsWith("```"))
        {
            code = code.Substring(0, code.Length - 3);
        }

        return code.Trim();
    }

    /// <summary>
    /// Regenerates code with contract violation feedback using response chaining.
    /// </summary>
    /// <param name="task">The task to regenerate code for.</param>
    /// <param name="validationResult">Validation errors and warnings from compilation.</param>
    /// <param name="contractViolations">Optional list of specific contract violations detected.</param>
    /// <returns>The regenerated C# code that addresses violations and errors.</returns>
    /// <remarks>
    /// Contract-Aware Regeneration with Response Chaining:
    /// This method maintains the response chain from the original generation, ensuring the model
    /// remembers the original intent and contract requirements while fixing violations.
    /// 
    /// The regeneration process prioritizes:
    /// 1. Contract violations (signature mismatches, missing implementations)
    /// 2. Compilation errors (syntax, type errors)
    /// 3. Warnings (style, nullability)
    /// 
    /// By chaining from the previous response, the model can understand what was attempted
    /// originally and make surgical fixes rather than complete rewrites, preserving working
    /// code while addressing specific issues.
    /// 
    /// Fallback Behavior:
    /// If no contract catalog is configured, delegates to regular error-based regeneration
    /// while still maintaining response chain context.
    /// </remarks>
    public async Task<string> RegenerateCodeWithContractFeedbackAsync(
        TaskNode task,
        ValidationResult validationResult,
        List<string>? contractViolations = null)
    {
        if (_contractCatalog == null || _promptContextBuilder == null)
        {
            return await RegenerateCodeWithErrorsAsync(task, validationResult);
        }

        var errorsText = string.Join("\n", FilterSpecificErrors(validationResult.Errors));
        var warningsText = string.Join("\n", validationResult.Warnings);
        var violationsText = contractViolations != null 
            ? string.Join("\n", contractViolations) 
            : string.Empty;

        // Get previous response ID for chaining
        string? previousResponseId = _responseChain.ContainsKey(task.Id) ? _responseChain[task.Id] : null;

        var systemPrompt = @"You are an expert C# programmer fixing code to comply with FROZEN CONTRACTS.

Your task is to fix ALL errors while maintaining EXACT compliance with contracts.

FIX PRIORITY:
1. Contract violations (signature mismatches, missing members, wrong types)
2. Compilation errors (syntax, missing usings, type errors)
3. Warnings (style, nullable, etc.)

CRITICAL RULES:
- DO NOT change contract signatures - change your implementation instead
- ADD missing interface/abstract method implementations with EXACT signatures
- USE only valid enum members from the contract
- FULLY QUALIFY ambiguous type references
- DO NOT remove or modify frozen contract types";

        var userPrompt = $@"Fix the following code to comply with frozen contracts and resolve all errors:

CURRENT CODE:
{task.GeneratedCode}

COMPILATION ERRORS:
{errorsText}

WARNINGS:
{warningsText}

CONTRACT VIOLATIONS:
{violationsText}

FIX INSTRUCTIONS:
1. Address each error systematically
2. Ensure all interface methods are implemented with EXACT signatures
3. Ensure all abstract methods are overridden with EXACT signatures
4. Use only valid enum members
5. Fully qualify any ambiguous type references

Return ONLY the fixed C# code without any markdown formatting or explanations.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Use Codex Responses API with chaining
                var (responseId, content) = await CallCodexResponsesAsync(messages, previousResponseId);
                
                // Update response ID for this task
                _responseChain[task.Id] = responseId;
                
                var fixedCode = CleanGeneratedCode(content);
                task.TypeContract = ExtractTypeContract(fixedCode);

                return fixedCode;
            }
            catch (HttpRequestException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"HTTP error during code regeneration (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        throw new InvalidOperationException("Failed to regenerate code after multiple attempts");
    }
}
