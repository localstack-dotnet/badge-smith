using Microsoft.Extensions.Caching.Memory;

namespace BadgeSmith.Api.Core.Caching;

internal sealed class MemoryAppCache : IAppCache, IDisposable
{
    private readonly IMemoryCache _memoryCache;

    public MemoryAppCache(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (_memoryCache.TryGetValue(key, out var obj) && obj is T v)
        {
            value = v;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        using var entry = _memoryCache.CreateEntry(key);
        entry.AbsoluteExpirationRelativeToNow = ttl;
        entry.Value = value;
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }
}
