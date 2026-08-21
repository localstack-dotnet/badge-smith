using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BadgeSmith.Api.Core.Caching;

namespace BadgeSmith.Api.Performance.Tests.TestHelpers;

/// <summary>
/// Always responds with one scripted status/body pair, building a fresh response per request because
/// provider services dispose responses after reading.
/// </summary>
/// <param name="statusCode">The scripted upstream status code.</param>
/// <param name="content">The scripted upstream body.</param>
/// <param name="etag">The optional scripted ETag header.</param>
internal sealed class ScriptedUpstreamHandler(HttpStatusCode statusCode, string content, EntityTagHeaderValue? etag = null) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };

        response.Headers.ETag = etag;
        return Task.FromResult(response);
    }
}

/// <summary>
/// Minimal deterministic <see cref="IAppCache"/> double supporting per-iteration resets.
/// </summary>
internal sealed class ScriptedCache : IAppCache
{
    private readonly Dictionary<string, object?> _entries = new(StringComparer.Ordinal);

    public int Hits { get; private set; }

    public void Seed(string key, object? entry) => _entries[key] = entry;

    public void Reset()
    {
        _entries.Clear();
        Hits = 0;
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (_entries.TryGetValue(key, out var entry) && entry is T typed)
        {
            Hits++;
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl) => _entries[key] = value;
}
