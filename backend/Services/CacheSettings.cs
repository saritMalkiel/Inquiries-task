namespace Requests.Services;

/// <summary>
/// Bound from the "Cache" configuration section.
/// </summary>
public sealed class CacheSettings
{
    public int TtlSeconds { get; set; } = 60;
}