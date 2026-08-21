using BadgeSmith.Api.Core.Caching;

namespace BadgeSmith.Api.Tests.TestHelpers;

internal sealed class RecordingAppCache : IAppCache
{
    public sealed record SetRecord(string Key, object? Entry, TimeSpan Ttl);

    private readonly Dictionary<string, object?> _seededEntries = [with(StringComparer.Ordinal)];

    public List<SetRecord> Sets { get; } = [];

    public RecordingAppCache Seed(string key, object? entry)
    {
        _seededEntries[key] = entry;
        return this;
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (_seededEntries.TryGetValue(key, out var entry) && entry is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        Sets.Add(new SetRecord(key, value, ttl));
    }
}
