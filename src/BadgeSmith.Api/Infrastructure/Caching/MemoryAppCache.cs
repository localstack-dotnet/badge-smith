using Microsoft.Extensions.Caching.Memory;

namespace BadgeSmith.Api.Infrastructure.Caching;

internal sealed class MemoryAppCache : IAppCache, IDisposable
{
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (Cache.TryGetValue(key, out var obj) && obj is T v)
        {
            value = v;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        using var entry = Cache.CreateEntry(key);
        entry.AbsoluteExpirationRelativeToNow = ttl;
        entry.Value = value;
    }

    public void Dispose()
    {
        Cache.Dispose();
    }
}
