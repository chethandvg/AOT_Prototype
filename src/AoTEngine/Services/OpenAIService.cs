using AoTEngine.Models;
using Newtonsoft.Json;
using OpenAI.Chat;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AoTEngine.Services;

/// <summary>
/// Service for interacting with OpenAI API.
/// </summary>
public class OpenAIService
{
    private readonly ChatClient _chatClient;
    private readonly ChatClient _codeGenChatClient; // Separate client for code generation with codex-max
    private const int MaxRetries = 3;

    public OpenAIService(string apiKey, string model = "gpt-4")
    {
        _chatClient = new ChatClient(model, apiKey);
        // Use gpt-4.5-codex-max for code generation phase
        _codeGenChatClient = new ChatClient("gpt-5.2", apiKey);
    }

    /// <summary>
    /// Decomposes a user request into atomic subtasks with dependencies.
    /// </summary>
    public async Task<TaskDecompositionResponse> DecomposeTaskAsync(TaskDecompositionRequest request)
    {
        var systemPrompt = @"You are an expert at decomposing complex programming tasks into atomic subtasks.
Your output must be valid JSON following this structure:
{
  ""description"": ""Overall description"",
  ""tasks"": [
    {
      ""id"": ""task1"",
      ""description"": ""Task description"",
      ""dependencies"": [],
      ""context"": ""Context needed for this task"",
      ""namespace"": ""ProjectName.Feature"",
      ""expectedTypes"": [""ClassName1"", ""InterfaceName1""],
      ""consumedTypes"": {
        ""task2"": [""DependentClass"", ""IService""]
      },
      ""requiredPackages"": [""Newtonsoft.Json"", ""System.Net.Http""]
    }
  ]
}

Rules:
1. Create a DAG (Directed Acyclic Graph) of tasks
2. Each task should be atomic and independently executable
3. Specify dependencies using task IDs
4. Include namespace structure for proper organization (e.g., ProjectName.Services, ProjectName.Models)
5. List expected type names (classes, interfaces, enums) for each task
6. Map consumed types from dependencies with task ID -> type names
7. Specify required NuGet packages or assemblies (if needed beyond System.*)
8. Tasks with no dependencies can run in parallel
9. Ensure no circular dependencies
10. Use meaningful namespace hierarchies to organize code";

        var userPrompt = $@"Decompose this request into atomic subtasks:

Request: {request.OriginalRequest}
Context: {request.Context}

Return ONLY valid JSON with the structure specified.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var completion = await _chatClient.CompleteChatAsync(messages);
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

                var content = contentParts[0].Text;

                // Try to parse the JSON response
                var response = JsonConvert.DeserializeObject<TaskDecompositionResponse>(content);
                if (response?.Tasks != null && response.Tasks.Count > 0)
                {
                    return response;
                }
            }
            catch (HttpRequestException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"HTTP error during task decomposition (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (JsonException ex)
            {
                if (attempt == MaxRetries - 1) throw;
                Console.WriteLine($"JSON parsing error during task decomposition (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        throw new InvalidOperationException("Failed to decompose task after multiple attempts");
    }

    /// <summary>
    /// Generates code for a specific task.
    /// </summary>
    public async Task<string> GenerateCodeAsync(TaskNode task, Dictionary<string, TaskNode> completedTasks)
    {
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine($"Task: {task.Description}");
        contextBuilder.AppendLine($"Context: {task.Context}");

        if (!string.IsNullOrEmpty(task.Namespace))
        {
            contextBuilder.AppendLine($"Target Namespace: {task.Namespace}");
        }

        if (task.ExpectedTypes.Any())
        {
            contextBuilder.AppendLine($"Expected Types to Generate: {string.Join(", ", task.ExpectedTypes)}");
        }

        // Include outputs from dependent tasks
        if (task.Dependencies.Any())
        {
            contextBuilder.AppendLine("\nDependent task outputs:");
            foreach (var depId in task.Dependencies.Where(depId => completedTasks.ContainsKey(depId)))
            {
                var depTask = completedTasks[depId];
                
                // Extract and use type contract (interface/API signatures)
                var contract = ExtractTypeContract(depTask.GeneratedCode);
                
                if (!string.IsNullOrWhiteSpace(contract))
                {
                    contextBuilder.AppendLine($"\n{depId} Type Contract (use these types):");
                    contextBuilder.AppendLine(contract);
                }
                
                // Include full code only for small snippets (< 500 chars)
                if (depTask.GeneratedCode.Length < 500)
                {
                    contextBuilder.AppendLine($"\n{depId} Full Implementation:");
                    contextBuilder.AppendLine(depTask.GeneratedCode);
                }
            }
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Use enhanced prompt on the 3rd attempt
                var systemPrompt = GetCodeGenerationSystemPrompt(attempt);
                var userPrompt = GetCodeGenerationUserPrompt(contextBuilder.ToString(), attempt, task);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                // Use gpt-4.5-codex-max for code generation
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

                var generatedCode = contentParts[0].Text.Trim();
                
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
    /// Regenerates code with validation errors as feedback.
    /// Filters out namespace and type not found errors as these will be resolved in batch validation.
    /// </summary>
    public async Task<string> RegenerateCodeWithErrorsAsync(TaskNode task, ValidationResult validationResult)
    {
        // Filter out namespace and type-related errors that will be resolved in batch validation
        var filteredErrors = FilterSpecificErrors(validationResult.Errors);
        
        // If all errors were filtered out, return the original code (errors will be fixed in batch validation)
        if (!filteredErrors.Any())
        {
            Console.WriteLine($"   ℹ️  All errors for task {task.Id} are namespace/type-related and will be resolved in batch validation");
            return task.GeneratedCode;
        }
        
        var errorsText = string.Join("\n", filteredErrors);
        var warningsText = string.Join("\n", validationResult.Warnings);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Use enhanced prompt on the 3rd attempt
                var systemPrompt = GetCodeRegenerationSystemPrompt(attempt);
                var userPrompt = GetCodeRegenerationUserPrompt(task.GeneratedCode, errorsText, warningsText, attempt);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                // Use gpt-4.5-codex-max for code regeneration
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

                var regeneratedCode = contentParts[0].Text.Trim();
                
                // Update type contract after regeneration
                task.TypeContract = ExtractTypeContract(regeneratedCode);
                
                return regeneratedCode;
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

    /// <summary>
    /// Gets the system prompt for code generation with enhanced instructions on the 3rd attempt.
    /// </summary>
    private string GetCodeGenerationSystemPrompt(int attempt)
    {
        if (attempt < 2) // Attempts 0 and 1
        {
            return @"You are an expert C# programmer. Generate clean, efficient, and well-documented C# code.
Include necessary using statements and create complete, runnable code snippets.
Follow best practices and ensure code is production-ready.
When dependencies are provided, use their contracts and types correctly.
Ensure type compatibility and proper namespacing.
Generate code that matches the specified namespace and expected types.";
        }
        else // Attempt 2 (3rd attempt) - Enhanced prompt
        {
            return @"You are a SENIOR EXPERT C# programmer with EXTENSIVE experience in .NET 9 and production-grade code.
This is a CRITICAL attempt - previous attempts have failed. Take EXTRA CARE to ensure SUCCESS.

CRITICAL REQUIREMENTS:
1. Generate COMPLETE, PRODUCTION-READY C# code with ALL necessary using statements
2. Ensure STRICT type compatibility across all references
3. Use EXACT namespace matching as specified
4. Implement ALL expected types with COMPLETE implementations
5. Follow C# best practices, SOLID principles, and modern .NET 9 patterns
6. Add comprehensive XML documentation comments for all public members
7. Include proper error handling and validation where appropriate
8. Ensure all dependency types are used EXACTLY as defined in their contracts
9. Verify all property types, method signatures, and return types are correct
10. Use nullable reference types appropriately for .NET 9

CODE QUALITY STANDARDS:
- Use async/await patterns correctly
- Implement IDisposable if managing resources
- Use modern C# features (records, pattern matching, etc.) where appropriate
- Ensure thread-safety if applicable
- Add defensive programming practices

CAREFULLY REVIEW your generated code for:
- Missing using directives
- Namespace mismatches
- Type incompatibilities
- Missing or incorrect member implementations
- Syntax errors
- Logic errors

This code will be validated automatically. ENSURE IT COMPILES and RUNS correctly.";
        }
    }

    /// <summary>
    /// Gets the user prompt for code generation with enhanced context on the 3rd attempt.
    /// </summary>
    private string GetCodeGenerationUserPrompt(string context, int attempt, TaskNode task)
    {
        if (attempt < 2) // Attempts 0 and 1
        {
            return $@"Generate C# code for the following:

{context}

Return ONLY the C# code without any markdown formatting or explanations.";
        }
        else // Attempt 2 (3rd attempt) - Enhanced prompt with examples
        {
            var enhancedPrompt = $@"⚠️ CRITICAL ATTEMPT #{attempt + 1} - Previous attempts failed. Maximum effort required.

TASK DETAILS:
{context}

IMPORTANT REMINDERS:
- This is attempt #{attempt + 1} of {MaxRetries}
- Previous attempts had issues - review requirements carefully
- This code will be combined with other tasks for validation
- Ensure ALL types, namespaces, and references are EXACT and CORRECT

EXPECTED OUTPUT FORMAT:
1. Start with ALL necessary using directives (using System; using System.Collections.Generic; etc.)
2. Define the namespace EXACTLY as specified: {task.Namespace ?? "ProjectName.Feature"}
3. Implement ALL expected types: {string.Join(", ", task.ExpectedTypes)}
4. Use file-scoped namespaces (namespace ProjectName.Feature;)
5. Add XML documentation for all public members

EXAMPLE STRUCTURE:
```csharp
using System;
using System.Collections.Generic;
// ... other using statements

namespace {task.Namespace ?? "ProjectName.Feature"};

/// <summary>
/// Description of the class
/// </summary>
public class YourClass
{{
    /// <summary>
    /// Description of property
    /// </summary>
    public string PropertyName {{ get; set; }}
    
    /// <summary>
    /// Description of method
    /// </summary>
    public void MethodName()
    {{{{
        // Implementation
    }}
}}
```

Now generate the COMPLETE, ERROR-FREE C# code. Return ONLY the code without markdown formatting or explanations.";
            
            return enhancedPrompt;
        }
    }

    /// <summary>
    /// Gets the system prompt for code regeneration with enhanced instructions on the 3rd attempt.
    /// </summary>
    private string GetCodeRegenerationSystemPrompt(int attempt)
    {
        if (attempt < 2) // Attempts 0 and 1
        {
            return @"You are an expert C# programmer. Fix the code based on the validation errors provided.
Generate clean, efficient, and well-documented C# code.
Include necessary using statements and create complete, runnable code snippets.";
        }
        else // Attempt 2 (3rd attempt) - Enhanced prompt
        {
            return @"You are a SENIOR EXPERT C# programmer specializing in DEBUGGING and ERROR RESOLUTION.
This is the FINAL attempt - previous fixes have FAILED. Apply MAXIMUM EFFORT.

CRITICAL MISSION:
Fix ALL validation errors with SURGICAL PRECISION. This is the last chance to get it right.

ERROR FIXING PROTOCOL:
1. CAREFULLY analyze EACH error message
2. Identify the ROOT CAUSE of each error
3. Fix errors systematically, starting with fundamental issues
4. Ensure fixes don't introduce new errors
5. Verify type compatibility across ALL references
6. Check for missing using directives
7. Validate namespace consistency
8. Ensure all member signatures are correct
9. Test logic flow mentally before generating

COMMON ERROR PATTERNS TO CHECK:
- Missing or incorrect using statements
- Namespace mismatches
- Type name typos or case sensitivity issues
- Missing properties, methods, or constructors
- Incorrect method signatures or return types
- Null reference issues
- Access modifier problems
- Interface implementation issues
- Generic type constraints

QUALITY ASSURANCE:
- The fixed code MUST compile without errors
- ALL validation errors MUST be resolved
- Code must maintain production quality
- Follow .NET 9 best practices
- Use modern C# patterns

This is your LAST CHANCE. Make it count.";
        }
    }

    /// <summary>
    /// Gets the user prompt for code regeneration with enhanced context on the 3rd attempt.
    /// </summary>
    private string GetCodeRegenerationUserPrompt(string code, string errorsText, string warningsText, int attempt)
    {
        if (attempt < 2) // Attempts 0 and 1
        {
            return $@"The following code has validation errors:

Code:
{code}

Errors:
{errorsText}

Warnings:
{warningsText}

Fix the code and return ONLY the corrected C# code without any markdown formatting or explanations.";
        }
        else // Attempt 2 (3rd attempt) - Enhanced prompt with detailed analysis
        {
            return $@"🚨 FINAL ATTEMPT #{attempt + 1} - This is your LAST CHANCE to fix the errors. 🚨

CURRENT CODE WITH ERRORS:
{code}

=== VALIDATION ERRORS (MUST ALL BE FIXED) ===
{errorsText}

=== WARNINGS (SHOULD BE ADDRESSED) ===
{warningsText}

SYSTEMATIC FIX APPROACH:
1. Read each error message carefully
2. Locate the exact line/type causing the error
3. Determine the root cause (missing using, wrong type, typo, etc.)
4. Apply the fix precisely
5. Ensure the fix doesn't break other parts of the code
6. Verify all types and namespaces are correct

ERROR ANALYSIS CHECKLIST:
☐ Are all required using statements present?
☐ Are all type names spelled correctly and match exactly?
☐ Are all namespaces correct and consistent?
☐ Are all required properties and methods implemented?
☐ Are method signatures correct (parameters, return types)?
☐ Are access modifiers appropriate?
☐ Are there any typos in member names?
☐ Are all dependencies correctly referenced?

CRITICAL REMINDER:
- This code failed validation {attempt + 1} times already
- Every error MUST be fixed in this attempt
- No new errors can be introduced
- The code MUST compile and validate successfully
- Previous attempts were insufficient - be MORE THOROUGH

Generate the COMPLETELY FIXED code now. Return ONLY the corrected C# code without any markdown formatting or explanations.";
        }
    }

    /// <summary>
    /// Filters out namespace and type-related errors that will be resolved during batch validation.
    /// </summary>
    private List<string> FilterSpecificErrors(List<string> errors)
    {
        var filteredErrors = new List<string>();
        
        foreach (var error in errors)
        {
            var errorLower = error.ToLowerInvariant();
            
            // Skip errors related to missing namespaces
            if (errorLower.Contains("namespace") && 
                (errorLower.Contains("could not be found") || 
                 errorLower.Contains("does not exist") ||
                 errorLower.Contains("not found")))
            {
                continue;
            }
            
            // Skip errors related to missing types
            if ((errorLower.Contains("type") || 
                 errorLower.Contains("class") || 
                 errorLower.Contains("interface") ||
                 errorLower.Contains("struct") ||
                 errorLower.Contains("enum") ||
                 errorLower.Contains("record")) && 
                (errorLower.Contains("could not be found") || 
                 errorLower.Contains("does not exist") ||
                 errorLower.Contains("not found") ||
                 errorLower.Contains("missing")))
            {
                continue;
            }
            
            // Skip "The type or namespace name" errors
            if (errorLower.Contains("the type or namespace name"))
            {
                continue;
            }
            
            // Skip "using directive" errors for missing namespaces
            if (errorLower.Contains("using") && errorLower.Contains("directive"))
            {
                continue;
            }
            
            // Keep all other errors
            filteredErrors.Add(error);
        }
        
        return filteredErrors;
    }

    /// <summary>
    /// Extracts type signatures, interfaces, and public contracts from generated code.
    /// This provides a lightweight contract for dependent tasks to reference.
    /// </summary>
    private string ExtractTypeContract(string code)
    {
        var contractBuilder = new System.Text.StringBuilder();
        
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot();
            
            // Extract using directives
            var usingDirectives = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(u => u.ToString().Trim())
                .Distinct();
            
            foreach (var usingDirective in usingDirectives)
            {
                contractBuilder.AppendLine(usingDirective);
            }
            
            if (usingDirectives.Any())
            {
                contractBuilder.AppendLine();
            }
            
            // Extract namespace declarations
            var namespaceDeclarations = root.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>();
            
            foreach (var namespaceDecl in namespaceDeclarations)
            {
                contractBuilder.AppendLine($"namespace {namespaceDecl.Name};");
                contractBuilder.AppendLine();
                
                // Extract type declarations (classes, interfaces, enums, records)
                var typeDeclarations = namespaceDecl.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>();
                
                foreach (var typeDecl in typeDeclarations)
                {
                    // Extract modifiers, keyword, identifier, and base types
                    var modifiers = string.Join(" ", typeDecl.Modifiers.Select(m => m.Text));
                    var keyword = typeDecl.Keyword.Text;
                    var identifier = typeDecl.Identifier.Text;
                    var baseList = typeDecl.BaseList?.ToString() ?? "";
                    
                    contractBuilder.AppendLine($"{modifiers} {keyword} {identifier}{baseList}");
                    contractBuilder.AppendLine("{");
                    
                    // Extract public members (properties, methods, events)
                    var publicMembers = typeDecl.Members
                        .Where(m => m.Modifiers.Any(mod => mod.Text == "public"));
                    
                    foreach (var member in publicMembers)
                    {
                        var memberSignature = ExtractMemberSignature(member);
                        if (!string.IsNullOrEmpty(memberSignature))
                        {
                            contractBuilder.AppendLine($"    {memberSignature}");
                        }
                    }
                    
                    contractBuilder.AppendLine("}");
                    contractBuilder.AppendLine();
                }
            }
        }
        catch
        {
            // Fallback: return a comment if parsing fails
            return $"// Contract extraction failed - full code required\n// Code length: {code.Length} chars";
        }
        
        return contractBuilder.ToString();
    }

    /// <summary>
    /// Extracts the signature of a class member (property, method, event, etc.)
    /// </summary>
    private string ExtractMemberSignature(MemberDeclarationSyntax member)
    {
        return member switch
        {
            PropertyDeclarationSyntax property => 
                $"{property.Modifiers} {property.Type} {property.Identifier} {{ {string.Join(" ", property.AccessorList?.Accessors.Select(a => a.Keyword.Text + ";") ?? [])} }}",
            
            MethodDeclarationSyntax method =>
                $"{method.Modifiers} {method.ReturnType} {method.Identifier}{method.ParameterList};",
            
            FieldDeclarationSyntax field =>
                $"{field.Modifiers} {field.Declaration};",
            
            EventDeclarationSyntax eventDecl =>
                $"{eventDecl.Modifiers} event {eventDecl.Type} {eventDecl.Identifier};",
            
            ConstructorDeclarationSyntax ctor =>
                $"{ctor.Modifiers} {ctor.Identifier}{ctor.ParameterList};",
            
            _ => member.ToString().Split('{')[0].Trim() + ";"
        };
    }

    /// <summary>
    /// Queries OpenAI to get the latest stable and compatible package versions for .NET 9.
    /// </summary>
    /// <param name="packageNames">List of package names to get versions for</param>
    /// <returns>Dictionary mapping package names to their recommended versions</returns>
    public async Task<Dictionary<string, string>> GetPackageVersionsAsync(List<string> packageNames)
    {
        if (packageNames == null || !packageNames.Any())
        {
            return new Dictionary<string, string>();
        }

        var systemPrompt = @"You are a .NET package version expert. For each NuGet package name provided, return the latest stable version that is compatible with .NET 9.

Your response MUST be valid JSON with this exact structure:
{
  ""packages"": [
    { ""name"": ""PackageName"", ""version"": ""X.Y.Z"", ""compatible"": true }
  ]
}

Rules:
1. Only return stable versions (no pre-release, alpha, beta, rc versions)
2. Ensure compatibility with .NET 9 / .NET Standard 2.0+
3. Use the most recent stable version available
4. Set compatible to false if the package doesn't support .NET 9
5. For Microsoft.Extensions.* packages, use version 9.0.0 for .NET 9 compatibility
6. For Entity Framework Core packages, use version 9.0.0 for .NET 9 compatibility
7. Return ONLY the JSON, no explanations or additional text";

        var packageList = string.Join("\n", packageNames.Select(p => $"- {p}"));
        var userPrompt = $@"Get the latest stable .NET 9 compatible versions for these NuGet packages:

{packageList}

Return ONLY valid JSON.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var completion = await _chatClient.CompleteChatAsync(messages);
                var contentParts = completion.Value.Content;
                
                if (contentParts == null || contentParts.Count == 0)
                {
                    if (attempt == MaxRetries - 1)
                    {
                        Console.WriteLine("⚠️  OpenAI returned no content for package versions, using fallback versions");
                        return GetFallbackVersions(packageNames);
                    }
                    await Task.Delay(1000 * (attempt + 1));
                    continue;
                }

                var content = contentParts[0].Text.Trim();
                
                // Parse the JSON response
                var response = JsonConvert.DeserializeObject<PackageVersionResponse>(content);
                if (response?.Packages != null && response.Packages.Any())
                {
                    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pkg in response.Packages.Where(p => p.Compatible))
                    {
                        result[pkg.Name] = pkg.Version;
                    }
                    
                    // Fill in any missing packages with fallback versions
                    foreach (var packageName in packageNames)
                    {
                        if (!result.ContainsKey(packageName))
                        {
                            result[packageName] = GetFallbackVersion(packageName);
                        }
                    }
                    
                    return result;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP error getting package versions (attempt {attempt + 1}): {ex.Message}");
                if (attempt == MaxRetries - 1)
                {
                    return GetFallbackVersions(packageNames);
                }
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON parsing error for package versions (attempt {attempt + 1}): {ex.Message}");
                if (attempt == MaxRetries - 1)
                {
                    return GetFallbackVersions(packageNames);
                }
                await Task.Delay(1000 * (attempt + 1));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting package versions (attempt {attempt + 1}): {ex.Message}");
                if (attempt == MaxRetries - 1)
                {
                    return GetFallbackVersions(packageNames);
                }
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        return GetFallbackVersions(packageNames);
    }

    /// <summary>
    /// Gets fallback versions for packages when OpenAI call fails.
    /// </summary>
    private Dictionary<string, string> GetFallbackVersions(List<string> packageNames)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var packageName in packageNames)
        {
            result[packageName] = KnownPackageVersions.GetVersionWithFallback(packageName);
        }
        return result;
    }

    /// <summary>
    /// Gets a fallback version for a single package.
    /// </summary>
    private string GetFallbackVersion(string packageName)
    {
        return KnownPackageVersions.GetVersionWithFallback(packageName);
    }
}

/// <summary>
/// Response model for package version queries.
/// </summary>
internal class PackageVersionResponse
{
    [JsonProperty("packages")]
    public List<PackageVersionInfo> Packages { get; set; } = new();
}

/// <summary>
/// Individual package version info.
/// </summary>
internal class PackageVersionInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;
    
    [JsonProperty("compatible")]
    public bool Compatible { get; set; } = true;
}
