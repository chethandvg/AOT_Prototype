using AoTEngine.Models;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing contract-aware code generation methods.
/// These methods use the Contract-First approach to prevent type mismatches and missing members.
/// </summary>
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
    /// Generates code for a task using contract-aware context.
    /// This method uses frozen contracts to prevent type mismatches and missing members.
    /// </summary>
    public async Task<string> GenerateCodeWithContractsAsync(
        TaskNode task,
        Dictionary<string, TaskNode> completedTasks)
    {
        // If no contract catalog is set, fall back to regular code generation
        if (_contractCatalog == null || _promptContextBuilder == null)
        {
            return await GenerateCodeAsync(task, completedTasks);
        }

        // Build enhanced context with contracts
        var enhancedContext = _promptContextBuilder.BuildCodeGenerationContext(task, completedTasks);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var systemPrompt = GetContractAwareSystemPrompt(attempt);
                var userPrompt = GetContractAwareUserPrompt(enhancedContext, attempt, task);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                var completion = await _codeGenChatClient.CompleteChatAsync(messages);
                var contentParts = completion.Value.Content;

                if (contentParts == null || contentParts.Count == 0)
                {
                    if (attempt == MaxRetries - 1)
                    {
                        throw new InvalidOperationException("OpenAI chat completion returned no content.");
                    }
                    await Task.Delay(1000 * (attempt + 1));
                    continue;
                }

                var generatedCode = CleanGeneratedCode(contentParts[0].Text);

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
                        enhancedContext += $"\n\n/* CONTRACT VIOLATIONS TO FIX:\n{string.Join("\n", contractViolations)}\n*/";
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
    /// Regenerates code with contract violation feedback.
    /// </summary>
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
                var completion = await _codeGenChatClient.CompleteChatAsync(messages);
                var contentParts = completion.Value.Content;

                if (contentParts == null || contentParts.Count == 0)
                {
                    if (attempt == MaxRetries - 1)
                    {
                        throw new InvalidOperationException("OpenAI chat completion returned no content.");
                    }
                    await Task.Delay(1000 * (attempt + 1));
                    continue;
                }

                var fixedCode = CleanGeneratedCode(contentParts[0].Text);
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
