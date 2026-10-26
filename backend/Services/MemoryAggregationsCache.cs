using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Requests.Models;

namespace Requests.Services;

/// <summary>
/// Single-slot in-memory cache for the aggregations DTO.
/// Registered as a Singleton, so the entry and the rebuild gate are shared
/// across requests.
/// </summary>
public sealed class MemoryAggregationsCache : IAggregationsCache
{
    private const string CacheKey = "aggregations";

    private readonly IMemoryCache _cache;
    private readonly CacheSettings _settings;
    private readonly SemaphoreSlim _rebuildGate = new(1, 1);

    public MemoryAggregationsCache(IMemoryCache cache, IOptions<CacheSettings> settings)
    {
        _cache = cache;
        _settings = settings.Value;
    }

    public bool TryGet(out AggregationsDto? value) =>
        _cache.TryGetValue(CacheKey, out value);

    public void Set(AggregationsDto value) =>
        _cache.Set(CacheKey, value, TimeSpan.FromSeconds(_settings.TtlSeconds));

    public void Invalidate() => _cache.Remove(CacheKey);

    public async Task<IDisposable> AcquireRebuildLockAsync(CancellationToken ct = default)
    {
        await _rebuildGate.WaitAsync(ct);
        return new Releaser(_rebuildGate);
    }

    /// <summary>
    /// Releases the gate exactly once, even if Dispose is called twice.
    /// </summary>
    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _gate;

        public Releaser(SemaphoreSlim gate) => _gate = gate;

        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }
}