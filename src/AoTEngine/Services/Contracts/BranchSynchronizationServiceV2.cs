using AoTEngine.Models;

namespace AoTEngine.Services.Contracts;

/// <summary>
/// Service for synchronizing contracts across parallel decomposition branches.
/// Uses ReaderWriterLockSlim for thread-safe operations and optimistic concurrency.
/// </summary>
public class BranchSynchronizationServiceV2 : IDisposable
{
    private readonly Dictionary<string, VersionedContract> _contractRegistry = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Dictionary<string, int> _contractVersions = new();
    private bool _disposed = false;

    /// <summary>
    /// Registers a contract with version tracking.
    /// </summary>
    public async Task<ContractRegistrationResult> RegisterContractAsync(
        string typeName,
        string content,
        string ns,
        int maxRetries = 3)
    {
        var result = new ContractRegistrationResult();
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                _lock.EnterUpgradeableReadLock();
                try
                {
                    var key = $"{ns}.{typeName}";
                    var contentHash = ComputeContentHash(content);

                    // Check for existing contract
                    if (_contractRegistry.TryGetValue(key, out var existing))
                    {
                        // Check for conflicts
                        if (existing.ContentHash != contentHash)
                        {
                            result.ConflictType = ContractConflictType.IncompatibleDefinition;
                            result.ResolutionStrategy = ResolutionStrategy.KeepExisting;
                            result.Warnings.Add($"Contract for {key} already exists with different content");
                            result.Success = false;
                            return await Task.FromResult(result);
                        }

                        // Same contract, return existing
                        result.RegisteredContract = existing;
                        result.Success = true;
                        return await Task.FromResult(result);
                    }

                    // Register new contract
                    _lock.EnterWriteLock();
                    try
                    {
                        var version = _contractVersions.TryGetValue(key, out var v) ? v + 1 : 1;
                        _contractVersions[key] = version;

                        var contract = new VersionedContract
                        {
                            Version = version,
                            Content = content,
                            TypeName = typeName,
                            Namespace = ns,
                            CreatedAt = DateTime.UtcNow,
                            ContentHash = contentHash
                        };

                        _contractRegistry[key] = contract;
                        result.RegisteredContract = contract;
                        result.Success = true;

                        return await Task.FromResult(result);
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                }
                finally
                {
                    _lock.ExitUpgradeableReadLock();
                }
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to register contract after {maxRetries} retries: {ex.Message}";
                    return result;
                }

                await Task.Delay(100 * retryCount); // Exponential backoff
            }
        }

        result.Success = false;
        result.ErrorMessage = "Max retries exceeded";
        return result;
    }

    /// <summary>
    /// Synchronizes contracts across branches.
    /// </summary>
    public async Task<SynchronizationResult> SynchronizeContractsAsync(Dictionary<string, string> proposedContracts)
    {
        var result = new SynchronizationResult { Success = true };

        foreach (var kvp in proposedContracts)
        {
            // Validate key format (should contain at least one dot for namespace)
            if (!kvp.Key.Contains('.'))
            {
                result.Conflicts.Add(new ContractConflict
                {
                    TypeName = kvp.Key,
                    ConflictType = ContractConflictType.IncompatibleDefinition,
                    ResolutionStrategy = ResolutionStrategy.RequiresManualReview
                });
                continue;
            }

            var parts = kvp.Key.Split('.');
            var ns = string.Join('.', parts.Take(parts.Length - 1));
            var typeName = parts.Last();

            var regResult = await RegisterContractAsync(typeName, kvp.Value, ns);

            if (regResult.Success && regResult.RegisteredContract != null)
            {
                result.SynchronizedVersions[kvp.Key] = regResult.RegisteredContract.Version;
            }
            else if (regResult.ConflictType.HasValue)
            {
                result.Conflicts.Add(new ContractConflict
                {
                    TypeName = typeName,
                    ConflictType = regResult.ConflictType.Value,
                    ResolutionStrategy = regResult.ResolutionStrategy ?? ResolutionStrategy.RequiresManualReview
                });
            }
        }

        result.Success = result.Conflicts.Count == 0;
        if (!result.Success)
        {
            result.ErrorMessage = $"Found {result.Conflicts.Count} contract conflicts";
        }

        return result;
    }

    /// <summary>
    /// Gets a versioned snapshot of all contracts.
    /// </summary>
    public Dictionary<string, VersionedContract> GetContractSnapshot()
    {
        _lock.EnterReadLock();
        try
        {
            return new Dictionary<string, VersionedContract>(_contractRegistry);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a specific contract by key.
    /// </summary>
    public VersionedContract? GetContract(string key)
    {
        _lock.EnterReadLock();
        try
        {
            return _contractRegistry.TryGetValue(key, out var contract) ? contract : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Computes a simple hash of contract content.
    /// </summary>
    private string ComputeContentHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Clears all contracts (for testing).
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _contractRegistry.Clear();
            _contractVersions.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _lock?.Dispose();
            }
            _disposed = true;
        }
    }
}
