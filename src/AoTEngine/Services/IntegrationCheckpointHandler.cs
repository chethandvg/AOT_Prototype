using AoTEngine.Models;
using System.Text;

namespace AoTEngine.Services;

/// <summary>
/// Information about a conflict that requires manual resolution.
/// </summary>
public class ConflictReport
{
    /// <summary>
    /// The conflict that needs resolution.
    /// </summary>
    public TypeConflict Conflict { get; set; } = null!;

    /// <summary>
    /// Human-readable description of the conflict.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Diff of the conflicting declarations.
    /// </summary>
    public string DeclarationDiff { get; set; } = string.Empty;

    /// <summary>
    /// Recommended resolution.
    /// </summary>
    public string RecommendedResolution { get; set; } = string.Empty;

    /// <summary>
    /// Available resolution options.
    /// </summary>
    public List<ConflictResolutionOption> Options { get; set; } = new();
}

/// <summary>
/// A resolution option that can be selected by the user.
/// </summary>
public class ConflictResolutionOption
{
    /// <summary>
    /// Option identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of this option.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The resolution type for this option.
    /// </summary>
    public ConflictResolution Resolution { get; set; }
}

/// <summary>
/// Result of a manual checkpoint interaction.
/// </summary>
public class CheckpointResult
{
    /// <summary>
    /// Whether to continue with the merge.
    /// </summary>
    public bool Continue { get; set; }

    /// <summary>
    /// Selected resolutions for each conflict.
    /// </summary>
    public Dictionary<string, ConflictResolution> Resolutions { get; set; } = new();

    /// <summary>
    /// Whether to abort the entire merge operation.
    /// </summary>
    public bool Abort { get; set; }
}

/// <summary>
/// Handles manual checkpoints during code integration for conflicts that require human intervention.
/// </summary>
public class IntegrationCheckpointHandler
{
    private readonly bool _interactive;

    public IntegrationCheckpointHandler(bool interactive = true)
    {
        _interactive = interactive;
    }

    /// <summary>
    /// Creates a handler with optional interactive mode.
    /// </summary>
    /// <param name="userInteractionService">Reserved for future use with custom user interaction implementations.</param>
    /// <param name="interactive">Whether to enable interactive prompts.</param>
    public IntegrationCheckpointHandler(UserInteractionService? userInteractionService = null, bool interactive = true)
        : this(interactive)
    {
        // UserInteractionService parameter kept for API compatibility but not currently used.
        // Interactive prompts use Console directly. Future implementations could delegate to this service.
    }

    /// <summary>
    /// Generates a conflict report for the given conflicts.
    /// </summary>
    public List<ConflictReport> GenerateConflictReports(List<TypeConflict> conflicts)
    {
        var reports = new List<ConflictReport>();

        foreach (var conflict in conflicts)
        {
            var report = new ConflictReport
            {
                Conflict = conflict,
                Description = GenerateDescription(conflict),
                DeclarationDiff = GenerateDeclarationDiff(conflict),
                RecommendedResolution = GetRecommendationText(conflict.SuggestedResolution),
                Options = GenerateOptions(conflict)
            };

            reports.Add(report);
        }

        return reports;
    }

    /// <summary>
    /// Formats conflict reports for console display.
    /// </summary>
    public string FormatConflictReportForDisplay(List<ConflictReport> reports)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════");
        sb.AppendLine("                    INTEGRATION CONFLICTS DETECTED                      ");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════");
        sb.AppendLine();

        for (int i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            sb.AppendLine($"Conflict {i + 1} of {reports.Count}:");
            sb.AppendLine($"  Type: {report.Conflict.FullyQualifiedName}");
            sb.AppendLine($"  Description: {report.Description}");
            sb.AppendLine();
            sb.AppendLine("  Declarations:");
            sb.AppendLine(IndentText(report.DeclarationDiff, "    "));
            sb.AppendLine();
            sb.AppendLine($"  Recommended: {report.RecommendedResolution}");
            sb.AppendLine();
            sb.AppendLine("  Available options:");
            foreach (var option in report.Options)
            {
                sb.AppendLine($"    [{option.Id}] {option.Description}");
            }
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────────────");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Prompts the user to resolve conflicts interactively.
    /// </summary>
    public async Task<CheckpointResult> PromptForResolutionAsync(List<ConflictReport> reports)
    {
        var result = new CheckpointResult { Continue = true };

        if (!_interactive)
        {
            // In non-interactive mode, use recommended resolutions
            foreach (var report in reports)
            {
                result.Resolutions[report.Conflict.FullyQualifiedName] = report.Conflict.SuggestedResolution;
            }
            return result;
        }

        // Display conflict report
        Console.WriteLine(FormatConflictReportForDisplay(reports));

        // Prompt for each conflict
        foreach (var report in reports)
        {
            Console.WriteLine($"\nResolving conflict for: {report.Conflict.FullyQualifiedName}");
            Console.Write($"Enter option [{string.Join("/", report.Options.Select(o => o.Id))}] (default: {GetDefaultOptionId(report)}): ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            
            if (string.IsNullOrEmpty(input))
            {
                // Use default/recommended
                result.Resolutions[report.Conflict.FullyQualifiedName] = report.Conflict.SuggestedResolution;
            }
            else if (input == "A" || input == "ABORT")
            {
                result.Abort = true;
                result.Continue = false;
                return result;
            }
            else
            {
                var selectedOption = report.Options.FirstOrDefault(o => o.Id.Equals(input, StringComparison.OrdinalIgnoreCase));
                if (selectedOption != null)
                {
                    result.Resolutions[report.Conflict.FullyQualifiedName] = selectedOption.Resolution;
                }
                else
                {
                    Console.WriteLine("Invalid option, using recommended resolution.");
                    result.Resolutions[report.Conflict.FullyQualifiedName] = report.Conflict.SuggestedResolution;
                }
            }
        }

        Console.WriteLine("\nConflict resolution complete. Continuing with merge...");
        return result;
    }

    /// <summary>
    /// Checks if conflicts require manual intervention (non-trivial conflicts).
    /// </summary>
    public bool RequiresManualIntervention(List<TypeConflict> conflicts)
    {
        return conflicts.Any(c =>
            c.ConflictType == ConflictType.DuplicateMember ||
            c.SuggestedResolution == ConflictResolution.FailFast ||
            (c.ConflictType == ConflictType.AmbiguousTypeName && 
             c.SuggestedResolution != ConflictResolution.UseFullyQualifiedName));
    }

    private string GenerateDescription(TypeConflict conflict)
    {
        return conflict.ConflictType switch
        {
            ConflictType.DuplicateType => 
                $"Type '{conflict.FullyQualifiedName}' is defined in both task '{conflict.ExistingEntry.OwnerTaskId}' and task '{conflict.NewEntry.OwnerTaskId}'",
            ConflictType.DuplicateMember => 
                $"Type '{conflict.FullyQualifiedName}' has conflicting member definitions: {string.Join(", ", conflict.ConflictingMembers.Select(m => m.SignatureKey))}",
            ConflictType.AmbiguousTypeName => 
                $"Type name '{conflict.NewEntry.TypeName}' is ambiguous - exists in multiple namespaces",
            _ => $"Unknown conflict for type '{conflict.FullyQualifiedName}'"
        };
    }

    private string GenerateDeclarationDiff(TypeConflict conflict)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"--- Task: {conflict.ExistingEntry.OwnerTaskId} ---");
        if (conflict.ExistingEntry.SyntaxNode != null)
        {
            // Get first few lines of the declaration
            var lines = conflict.ExistingEntry.SyntaxNode.ToFullString().Split('\n').Take(5);
            sb.AppendLine(string.Join("\n", lines));
            sb.AppendLine("  ...");
        }
        else
        {
            sb.AppendLine($"  {conflict.ExistingEntry.Kind} {conflict.ExistingEntry.FullyQualifiedName}");
        }

        sb.AppendLine();
        sb.AppendLine($"--- Task: {conflict.NewEntry.OwnerTaskId} ---");
        if (conflict.NewEntry.SyntaxNode != null)
        {
            var lines = conflict.NewEntry.SyntaxNode.ToFullString().Split('\n').Take(5);
            sb.AppendLine(string.Join("\n", lines));
            sb.AppendLine("  ...");
        }
        else
        {
            sb.AppendLine($"  {conflict.NewEntry.Kind} {conflict.NewEntry.FullyQualifiedName}");
        }

        return sb.ToString();
    }

    private string GetRecommendationText(ConflictResolution resolution)
    {
        return resolution switch
        {
            ConflictResolution.KeepFirst => "Keep the first definition and discard the duplicate",
            ConflictResolution.MergeAsPartial => "Merge as partial class (both definitions will be combined)",
            ConflictResolution.RemoveDuplicate => "Remove the duplicate member(s)",
            ConflictResolution.FailFast => "Cannot auto-resolve - manual intervention required",
            ConflictResolution.UseFullyQualifiedName => "Use fully qualified names to disambiguate",
            _ => "Unknown resolution"
        };
    }

    private List<ConflictResolutionOption> GenerateOptions(TypeConflict conflict)
    {
        var options = new List<ConflictResolutionOption>();

        // Always available options
        options.Add(new ConflictResolutionOption
        {
            Id = "K",
            Description = "Keep first definition, discard duplicate",
            Resolution = ConflictResolution.KeepFirst
        });

        options.Add(new ConflictResolutionOption
        {
            Id = "R",
            Description = "Remove the duplicate",
            Resolution = ConflictResolution.RemoveDuplicate
        });

        // Partial class merge only for classes
        if (conflict.ExistingEntry.Kind == ProjectTypeKind.Class && 
            conflict.NewEntry.Kind == ProjectTypeKind.Class)
        {
            options.Add(new ConflictResolutionOption
            {
                Id = "P",
                Description = "Merge as partial class",
                Resolution = ConflictResolution.MergeAsPartial
            });
        }

        // Fully qualified option for ambiguity
        if (conflict.ConflictType == ConflictType.AmbiguousTypeName)
        {
            options.Add(new ConflictResolutionOption
            {
                Id = "Q",
                Description = "Use fully qualified names",
                Resolution = ConflictResolution.UseFullyQualifiedName
            });
        }

        // Fail/abort option
        options.Add(new ConflictResolutionOption
        {
            Id = "A",
            Description = "Abort the merge operation",
            Resolution = ConflictResolution.FailFast
        });

        return options;
    }

    private string GetDefaultOptionId(ConflictReport report)
    {
        return report.Conflict.SuggestedResolution switch
        {
            ConflictResolution.KeepFirst => "K",
            ConflictResolution.MergeAsPartial => "P",
            ConflictResolution.RemoveDuplicate => "R",
            ConflictResolution.UseFullyQualifiedName => "Q",
            _ => "K"
        };
    }

    private string IndentText(string text, string indent)
    {
        var lines = text.Split('\n');
        return string.Join("\n", lines.Select(line => indent + line));
    }
}
