using Microsoft.Extensions.Caching.Memory;

namespace BadgeSmith.Api.Infrastructure.Caching;

internal sealed class MemoryAppCache : IAppCache, IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out var obj) && obj is T v)
        {
            value = v;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        using var entry = _cache.CreateEntry(key);
        entry.AbsoluteExpirationRelativeToNow = ttl;
        entry.Value = value;
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
