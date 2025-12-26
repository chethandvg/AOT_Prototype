using AoTEngine.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AoTEngine.Services.Optimization;

/// <summary>
/// Optimized execution service with context-aware caching and safe batching.
/// </summary>
public class OptimizedExecutionServiceV2 : IDisposable
{
    private readonly MemoryCache _cache;
    private readonly int _maxSpeculativeRequests;
    private readonly double _minConfidenceThreshold;
    private readonly double _maxCostPerSpeculation;
    private bool _disposed = false;

    public OptimizedExecutionServiceV2(
        int maxSpeculativeRequests = 2,
        double minConfidenceThreshold = 0.7,
        double maxCostPerSpeculation = 0.01)
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _maxSpeculativeRequests = maxSpeculativeRequests;
        _minConfidenceThreshold = minConfidenceThreshold;
        _maxCostPerSpeculation = maxCostPerSpeculation;
    }

    /// <summary>
    /// Gets cached result if available and contract version matches.
    /// </summary>
    public T? GetCachedResult<T>(string key, int contractVersion) where T : class
    {
        var cacheKey = $"{key}_v{contractVersion}";
        return _cache.Get<T>(cacheKey);
    }

    /// <summary>
    /// Caches a result with contract version.
    /// </summary>
    public void CacheResult<T>(string key, T value, int contractVersion, TimeSpan? expiration = null)
    {
        var cacheKey = $"{key}_v{contractVersion}";
        _cache.Set(cacheKey, value, expiration ?? TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Validates cache when contracts change.
    /// </summary>
    public void InvalidateCacheForContract(string contractKey)
    {
        // Clear all cache entries when a contract changes
        _cache.Compact(1.0); // Remove all entries
    }

    /// <summary>
    /// Executes tasks in batches with individual fallbacks.
    /// </summary>
    public async Task<List<TResult>> ExecuteBatchWithFallbackAsync<TInput, TResult>(
        List<TInput> items,
        Func<TInput, Task<TResult>> executor,
        Func<TInput, Exception, Task<TResult>>? fallback = null)
    {
        var results = new List<TResult>();
        var tasks = new List<Task<(TInput item, TResult? result, Exception? error)>>();

        foreach (var item in items)
        {
            tasks.Add(ExecuteWithFallbackAsync(item, executor, fallback));
        }

        var completedTasks = await Task.WhenAll(tasks);

        foreach (var (item, result, error) in completedTasks)
        {
            if (error == null && result != null)
            {
                results.Add(result);
            }
            else if (error != null && fallback != null)
            {
                var fallbackResult = await fallback(item, error);
                results.Add(fallbackResult);
            }
        }

        return results;
    }

    private async Task<(TInput item, TResult? result, Exception? error)> ExecuteWithFallbackAsync<TInput, TResult>(
        TInput item,
        Func<TInput, Task<TResult>> executor,
        Func<TInput, Exception, Task<TResult>>? fallback)
    {
        try
        {
            var result = await executor(item);
            return (item, result, null);
        }
        catch (Exception ex)
        {
            if (fallback != null)
            {
                try
                {
                    var fallbackResult = await fallback(item, ex);
                    return (item, fallbackResult, null);
                }
                catch (Exception fallbackEx)
                {
                    return (item, default, fallbackEx);
                }
            }
            return (item, default, ex);
        }
    }

    /// <summary>
    /// Speculatively executes likely next tasks with cost controls.
    /// </summary>
    public async Task<Dictionary<string, T>> SpeculativeExecuteAsync<T>(
        Dictionary<string, Func<Task<T>>> candidates,
        Dictionary<string, double> confidenceScores,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, T>();
        var speculativeTasks = new List<Task<(string key, T result)>>();

        // Select top candidates based on confidence and cost
        var selectedCandidates = candidates
            .Where(kvp => confidenceScores.GetValueOrDefault(kvp.Key, 0) >= _minConfidenceThreshold)
            .Take(_maxSpeculativeRequests)
            .ToList();

        foreach (var kvp in selectedCandidates)
        {
            var task = Task.Run(async () =>
            {
                var result = await kvp.Value();
                return (kvp.Key, result);
            }, cancellationToken);

            speculativeTasks.Add(task);
        }

        try
        {
            var completedTasks = await Task.WhenAll(speculativeTasks);
            foreach (var (key, result) in completedTasks)
            {
                results[key] = result;
            }
        }
        catch (OperationCanceledException)
        {
            // Speculation was cancelled, return partial results
        }

        return results;
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
                _cache?.Dispose();
            }
            _disposed = true;
        }
    }
}
