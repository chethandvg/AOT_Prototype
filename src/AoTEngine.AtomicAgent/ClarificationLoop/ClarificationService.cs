using Microsoft.Extensions.Logging;

namespace AoTEngine.AtomicAgent.ClarificationLoop;

/// <summary>
/// Implements the Clarification Loop for ambiguity detection and resolution.
/// Section 6 of the architectural blueprint.
/// </summary>
public class ClarificationService
{
    private readonly ILogger<ClarificationService> _logger;
    private readonly bool _enableAmbiguityDetection;
    private readonly int _vagueTermsThreshold;

    private static readonly HashSet<string> VagueTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "manage", "handle", "process", "thing", "stuff", 
        "application", "solution", "program", "etc", "various"
    };

    public ClarificationService(
        ILogger<ClarificationService> logger,
        bool enableAmbiguityDetection = true,
        int vagueTermsThreshold = 3)
    {
        _logger = logger;
        _enableAmbiguityDetection = enableAmbiguityDetection;
        _vagueTermsThreshold = vagueTermsThreshold;
    }

    /// <summary>
    /// Analyzes a user request for ambiguity and prompts for clarification if needed.
    /// </summary>
    public async Task<string> AnalyzeAndClarifyAsync(string userRequest)
    {
        if (!_enableAmbiguityDetection)
        {
            return userRequest;
        }

        var vagueTermCount = CountVagueTerms(userRequest);
        var ambiguityLevel = DetermineAmbiguityLevel(userRequest, vagueTermCount);

        _logger.LogInformation("Ambiguity analysis: Level={Level}, VagueTerms={Count}", 
            ambiguityLevel, vagueTermCount);

        if (ambiguityLevel == AmbiguityLevel.High)
        {
            return RequestClarification(userRequest);
        }

        return userRequest;
    }

    /// <summary>
    /// Counts vague terms in the request.
    /// </summary>
    private int CountVagueTerms(string request)
    {
        var words = request.Split(new[] { ' ', ',', '.', '!', '?' }, 
            StringSplitOptions.RemoveEmptyEntries);
        
        return words.Count(w => VagueTerms.Contains(w));
    }

    /// <summary>
    /// Determines the ambiguity level of a request.
    /// </summary>
    private AmbiguityLevel DetermineAmbiguityLevel(string request, int vagueTermCount)
    {
        if (vagueTermCount >= _vagueTermsThreshold)
        {
            return AmbiguityLevel.High;
        }

        // Check for specific requirements
        var hasSpecifics = request.Contains("database", StringComparison.OrdinalIgnoreCase) ||
                          request.Contains("api", StringComparison.OrdinalIgnoreCase) ||
                          request.Contains("file", StringComparison.OrdinalIgnoreCase) ||
                          request.Contains("console", StringComparison.OrdinalIgnoreCase);

        return hasSpecifics ? AmbiguityLevel.Low : AmbiguityLevel.Medium;
    }

    /// <summary>
    /// Prompts the user for clarification.
    /// </summary>
    private string RequestClarification(string originalRequest)
    {
        Console.WriteLine();
        Console.WriteLine("⚠️  UNCERTAINTY DETECTED - Clarification Required");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine($"Original Request: {originalRequest}");
        Console.WriteLine();
        Console.WriteLine("Please clarify the following:");
        Console.WriteLine("1. What type of storage? (database/file/memory/none)");
        Console.Write("   Storage: ");
        var storage = Console.ReadLine()?.Trim() ?? "memory";

        Console.WriteLine("2. What type of interface? (console/api/web/none)");
        Console.Write("   Interface: ");
        var interfaceType = Console.ReadLine()?.Trim() ?? "console";

        Console.WriteLine("3. Any specific frameworks or libraries to use? (or 'none')");
        Console.Write("   Frameworks: ");
        var frameworks = Console.ReadLine()?.Trim() ?? "none";

        var enrichedRequest = $@"{originalRequest}

CLARIFICATIONS:
- Storage: {storage}
- Interface: {interfaceType}
- Frameworks: {frameworks}";

        _logger.LogInformation("Request enriched with clarifications");
        Console.WriteLine();
        
        return enrichedRequest;
    }
}

public enum AmbiguityLevel
{
    Low,
    Medium,
    High
}
