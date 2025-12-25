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
}
