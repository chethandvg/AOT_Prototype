using Newtonsoft.Json;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Partial class containing package version query methods.
/// </summary>
public partial class OpenAIService
{
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
2. Ensure compatibility with .NET 9. If a package does not have a .NET 9 target, then choose the latest stable version compatible with .NET Standard 2.0 or higher.
3. Use the most recent stable version available
4. Set compatible to false if the package doesn't support .NET 9 or .NET Standard 2.0+
5. For Microsoft.Extensions.* packages, use version 9.0.0 for .NET 9 compatibility
6. For Entity Framework Core packages, use version 9.0.0 for .NET 9 compatibility
7. Version strings must be valid semantic versions (e.g., 1.2.3 or 1.2.3.4)
8. Return ONLY the JSON, no explanations or additional text";

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
                    // Cap delay at 3 seconds max
                    await Task.Delay(Math.Min(1000 * (attempt + 1), 3000));
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
                        // Validate version format before using it (using static compiled regex)
                        if (!string.IsNullOrEmpty(pkg.Version) && VersionRegex.IsMatch(pkg.Version))
                        {
                            result[pkg.Name] = pkg.Version;
                        }
                        else
                        {
                            Console.WriteLine($"   ⚠️  Invalid version format '{pkg.Version}' for package '{pkg.Name}', using fallback");
                            result[pkg.Name] = GetFallbackVersion(pkg.Name);
                        }
                    }
                    
                    // Fill in any missing packages with fallback versions
                    foreach (var packageName in packageNames.Where(p => !result.ContainsKey(p)))
                    {
                        result[packageName] = GetFallbackVersion(packageName);
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
                // Cap delay at 3 seconds max to prevent excessive wait times
                await Task.Delay(Math.Min(1000 * (attempt + 1), 3000));
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON parsing error for package versions (attempt {attempt + 1}): {ex.Message}");
                if (attempt == MaxRetries - 1)
                {
                    return GetFallbackVersions(packageNames);
                }
                await Task.Delay(Math.Min(1000 * (attempt + 1), 3000));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting package versions (attempt {attempt + 1}): {ex.Message}");
                if (attempt == MaxRetries - 1)
                {
                    return GetFallbackVersions(packageNames);
                }
                await Task.Delay(Math.Min(1000 * (attempt + 1), 3000));
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
