namespace BadgeSmith.Api.Infrastructure.Caching;

internal interface IAppCache
{
    public bool TryGetValue<T>(string key, out T? value);

    public void Set<T>(string key, T value, TimeSpan ttl);
}
