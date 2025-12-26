using AoTEngine.Models;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing prompt generation methods for code generation and regeneration.
/// </summary>
public partial class OpenAIService
{
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
    /// Gets the system prompt for code regeneration with the "Code Repair Expert" pattern.
    /// Enhanced to clearly instruct the LLM to repair/fix rather than rewrite.
    /// </summary>
    private string GetCodeRegenerationSystemPrompt(int attempt)
    {
        if (attempt < 2) // Attempts 0 and 1
        {
            return @"You are a Code Repair Expert specializing in C# code correction.

ROLE: You are tasked with FIXING code that failed validation, not rewriting it from scratch.

APPROACH:
1. Analyze the error messages to identify specific issues
2. Make MINIMAL, TARGETED changes to fix each error
3. Preserve the original code structure and intent
4. Do NOT rewrite the entire logic if only specific fixes are needed
5. Ensure fixes don't introduce new errors

REPAIR GUIDELINES:
- Fix syntax errors precisely at the problematic location
- Add missing using directives at the top of the file
- Correct type mismatches and namespace issues
- Fix missing or incorrect method signatures
- Maintain the original design pattern and architecture

Generate clean, efficient, and well-documented C# code.
Include all necessary using statements and create complete, runnable code.";
        }
        else // Attempt 2 (3rd attempt) - Enhanced prompt
        {
            return @"You are a SENIOR CODE REPAIR EXPERT specializing in SURGICAL ERROR CORRECTION.
This is the FINAL attempt - previous fixes have FAILED. Apply MAXIMUM PRECISION.

CRITICAL MISSION:
You must REPAIR the existing code to fix ALL validation errors. Do NOT rewrite from scratch.

REPAIR PROTOCOL:
1. ANALYZE: Carefully examine EACH error message to identify the root cause
2. LOCATE: Pinpoint the exact line/type/member causing each error  
3. FIX: Apply MINIMAL, TARGETED changes to resolve each specific issue
4. VERIFY: Ensure your fix doesn't break other parts of the code
5. PRESERVE: Maintain the original code structure, intent, and design

ERROR CATEGORIES TO ADDRESS:
- Missing using statements → Add at top of file
- Namespace mismatches → Correct the namespace declaration
- Type name errors → Fix spelling, casing, or full qualification
- Missing members → Add required properties, methods, or constructors
- Signature mismatches → Correct parameter types, return types
- Access modifier issues → Adjust public/private/protected
- Interface implementation → Implement all required members

QUALITY STANDARDS:
- ALL validation errors MUST be resolved
- The repaired code MUST compile successfully
- Maintain production-quality code
- Follow .NET 9 best practices
- Preserve original functionality and intent

This is your LAST CHANCE. Apply surgical precision to repair this code.";
        }
    }

    /// <summary>
    /// Gets the user prompt for code regeneration with the "Code Repair Expert" pattern.
    /// Structures the prompt with sections: Original Intent, Failed Code, Error Log, Suggestions, Existing Types.
    /// </summary>
    /// <param name="taskDescription">Original task description (the intent).</param>
    /// <param name="taskNamespace">Target namespace for the task.</param>
    /// <param name="expectedTypes">Expected types to be generated.</param>
    /// <param name="code">The failed code to repair.</param>
    /// <param name="errorsText">Validation errors.</param>
    /// <param name="warningsText">Validation warnings.</param>
    /// <param name="suggestionsText">Suggestions for fixing the code.</param>
    /// <param name="existingTypeCode">Code from existing types that should be reused.</param>
    /// <param name="attempt">Current attempt number.</param>
    private string GetCodeRegenerationUserPrompt(
        string taskDescription, 
        string taskNamespace,
        List<string> expectedTypes,
        string code, 
        string errorsText, 
        string warningsText,
        string suggestionsText,
        string? existingTypeCode,
        int attempt)
    {
        var expectedTypesStr = expectedTypes.Any() ? string.Join(", ", expectedTypes) : "Not specified";
        var namespaceStr = !string.IsNullOrEmpty(taskNamespace) ? taskNamespace : "Not specified";
        
        // Build suggestions section if available
        var suggestionsSection = !string.IsNullOrWhiteSpace(suggestionsText) 
            ? $@"

=== INPUT 4: SUGGESTIONS (Recommended Fixes) ===
{suggestionsText}
" 
            : "";
        
        // Build existing types section if available (for duplicate type handling)
        var existingTypesSection = !string.IsNullOrWhiteSpace(existingTypeCode)
            ? $@"

=== INPUT 5: EXISTING TYPES (REUSE This Code - Do NOT Recreate) ===
⚠️ IMPORTANT: The following types already exist in other tasks. You MUST reuse them instead of creating new definitions.
- Do NOT redefine these types
- Only ADD new functionality if needed (extend, don't replace)
- Reference these types directly in your code
- If you need to add members, ensure they don't conflict with existing ones

```csharp
{existingTypeCode}
```
"
            : "";
        
        if (attempt < 2) // Attempts 0 and 1
        {
            return $@"You previously generated code that failed validation. Please REPAIR this code.

=== ORIGINAL INTENT (Task Requirements) ===
Task Description: {taskDescription}
Target Namespace: {namespaceStr}
Expected Types: {expectedTypesStr}

=== INPUT 1: FAILED CODE (Current Code That Needs Repair) ===
```csharp
{code}
```

=== INPUT 2: ERROR LOG (Specific Validation Errors To Fix) ===
```
{errorsText}
```

=== INPUT 3: WARNINGS (Should Be Addressed If Possible) ===
```
{warningsText}
```
{suggestionsSection}{existingTypesSection}
=== INSTRUCTIONS ===
Analyze the Error Log and modify the Failed Code to fix these SPECIFIC issues:
- Maintain the ORIGINAL INTENT as described above
- Do NOT rewrite the entire logic unless absolutely necessary
- Make MINIMAL, TARGETED changes to fix each error
- Preserve the original code structure and design
- Ensure all using statements are present
- Verify namespace and type names are correct
- If EXISTING TYPES are provided, REUSE them - do not recreate
- Follow the SUGGESTIONS if provided

Return ONLY the corrected C# code without any markdown formatting or explanations.";
        }
        else // Attempt 2 (3rd attempt) - Enhanced prompt with detailed analysis
        {
            return $@"🚨 FINAL REPAIR ATTEMPT #{attempt + 1} - Previous fixes were insufficient. Apply MAXIMUM PRECISION. 🚨

=== ORIGINAL INTENT (Task Requirements - MUST Be Preserved) ===
Task Description: {taskDescription}
Target Namespace: {namespaceStr}
Expected Types: {expectedTypesStr}

=== INPUT 1: FAILED CODE (Code That Has Failed {attempt + 1} Times) ===
```csharp
{code}
```

=== INPUT 2: ERROR LOG (ALL Errors MUST Be Fixed) ===
```
{errorsText}
```

=== INPUT 3: WARNINGS (Should Be Addressed) ===
```
{warningsText}
```
{suggestionsSection}{existingTypesSection}
=== SYSTEMATIC REPAIR APPROACH ===
For EACH error in the Error Log:
1. Read the error message carefully
2. Identify the exact location (line number, type, member)
3. Determine the root cause (missing import, wrong type, typo, etc.)
4. Apply the MINIMAL fix needed to resolve it
5. Verify the fix doesn't break other code

=== REPAIR CHECKLIST ===
☐ All required using statements present?
☐ Namespace declaration correct?
☐ All type names spelled correctly and match exactly?
☐ All required properties and methods implemented?
☐ Method signatures correct (parameters, return types)?
☐ Access modifiers appropriate?
☐ No typos in member names?
☐ All dependencies correctly referenced?
☐ Existing types reused (not recreated)?
☐ Suggestions followed where applicable?

=== CRITICAL REMINDER ===
- This code has failed validation {attempt + 1} times
- EVERY error in the Error Log MUST be fixed
- Make MINIMAL changes - don't rewrite unnecessarily  
- The repaired code MUST compile and validate successfully
- Previous repair attempts were insufficient - be MORE THOROUGH
- If EXISTING TYPES are provided, you MUST use them - do NOT recreate

Generate the COMPLETELY REPAIRED code now. Return ONLY the corrected C# code without any markdown formatting or explanations.";
        }
    }
}
