using AoTEngine.Models;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing uncertainty detection methods.
/// </summary>
public partial class UserInteractionService
{
    /// <summary>
    /// Detects potential uncertainties in task description.
    /// </summary>
    private List<string> DetectUncertainties(string description)
    {
        var uncertainties = new List<string>();
        var lowerDesc = description.ToLower();
        
        // Check for vague terms
        var vagueTerms = new Dictionary<string, string>
        {
            { "simple", "What level of simplicity is required? (basic/intermediate/advanced)" },
            { "complex", "What specific complexity is needed? Please provide details." },
            { "efficient", "What are the efficiency requirements? (time/space/both)" },
            { "fast", "What performance requirements should be met? (response time/throughput)" },
            { "secure", "What security requirements are needed? (authentication/authorization/encryption/validation)" },
            { "modern", "What modern practices or technologies should be used?" },
            { "best", "What criteria define 'best' for this implementation?" },
            { "appropriate", "What would be appropriate in this context?" },
            { "suitable", "What would be suitable for this use case?" }
        };
        
        var matchingTerms = vagueTerms.Where(term => lowerDesc.Contains(term.Key));
        foreach (var term in matchingTerms)
        {
            uncertainties.Add(term.Value);
        }
        
        // Check for missing specifications
        if (lowerDesc.Contains("database") && !lowerDesc.Contains("sql") && !lowerDesc.Contains("nosql") && 
            !lowerDesc.Contains("entity framework") && !lowerDesc.Contains("dapper"))
        {
            uncertainties.Add("Which database technology should be used? (SQL Server/PostgreSQL/MongoDB/etc.)");
        }
        
        if (lowerDesc.Contains("api") && !lowerDesc.Contains("rest") && !lowerDesc.Contains("graphql") && 
            !lowerDesc.Contains("grpc"))
        {
            uncertainties.Add("What type of API should be created? (REST/GraphQL/gRPC)");
        }
        
        if (lowerDesc.Contains("test") && !lowerDesc.Contains("unit") && !lowerDesc.Contains("integration") && 
            !lowerDesc.Contains("xunit") && !lowerDesc.Contains("nunit"))
        {
            uncertainties.Add("What type of tests are needed? (Unit tests/Integration tests/Both) and which framework? (xUnit/NUnit/MSTest)");
        }
        
        return uncertainties;
    }
}
