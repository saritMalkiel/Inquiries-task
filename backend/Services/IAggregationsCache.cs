using Requests.Models;

namespace Requests.Services;

public interface IAggregationsCache
{
    bool TryGet(out AggregationsDto? value);
    void Set(AggregationsDto value);
    void Invalidate();
    Task<IDisposable> AcquireRebuildLockAsync(CancellationToken ct = default);
}