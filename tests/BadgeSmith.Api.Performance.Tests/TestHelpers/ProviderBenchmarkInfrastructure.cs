using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BadgeSmith.Api.Core.Caching;
using BadgeSmith.Api.Features.GitHub;
using BadgeSmith.Api.Features.NuGet;

namespace BadgeSmith.Api.Performance.Tests.TestHelpers;

/// <summary>
/// Encodes one owned UTF-8 body per handler and returns fresh response/content objects per request,
/// because provider services dispose responses after reading. The body bytes are never mutated.
/// Raw Content-Type metadata is preserved; charset parsing remains part of the consumer's read.
/// </summary>
/// <param name="statusCode">The scripted upstream status code.</param>
/// <param name="content">The scripted upstream body.</param>
/// <param name="etag">The optional scripted ETag header.</param>
internal sealed class ScriptedUpstreamHandler(HttpStatusCode statusCode, string content, EntityTagHeaderValue? etag = null) : HttpMessageHandler
{
    private const string JsonContentType = "application/json; charset=utf-8";

    private readonly byte[] _body = Encoding.UTF8.GetBytes(content);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(_body),
        };

        response.Content.Headers.TryAddWithoutValidation("Content-Type", JsonContentType);
        response.Headers.ETag = etag;
        return Task.FromResult(response);
    }
}

/// <summary>
/// Minimal deterministic <see cref="IAppCache"/> double supporting per-invocation resets.
/// </summary>
internal sealed class ScriptedCache : IAppCache
{
    private readonly Dictionary<string, object?> _entries = new(StringComparer.Ordinal);

    public void Seed(string key, object? entry) => _entries[key] = entry;

    public void Reset()
    {
        _entries.Clear();
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (_entries.TryGetValue(key, out var entry) && entry is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl) => _entries[key] = value;
}

/// <summary>
/// Fixed synthetic corpora; both upstream schemas contain exactly the same names in the same order.
/// </summary>
/// <param name="NuGetPayload">The NuGet index JSON.</param>
/// <param name="GitHubPayload">The GitHub version-list JSON.</param>
/// <param name="StableVersion">The literal stable maximum.</param>
/// <param name="PrereleaseVersion">The literal maximum including prereleases.</param>
/// <param name="RangeVersion">The literal maximum in [1.0.0,2.0.0), when checked.</param>
internal sealed record ProviderBenchmarkCorpus(string NuGetPayload, string GitHubPayload, string StableVersion, string PrereleaseVersion, string? RangeVersion)
{
    private static readonly ProviderBenchmarkCorpus Original = new(
        """{"versions":["1.0.0","1.2.0","2.0.0-preview"]}""",
        """[{"name":"1.0.0"},{"name":"1.2.0"},{"name":"2.0.0-preview"}]""",
        "1.2.0", "2.0.0-preview", null);

    private static readonly ProviderBenchmarkCorpus ThirtyTwo = CreateThirtyTwo();

    public static ProviderBenchmarkCorpus ForSize(int size) => size switch
    {
        3 => Original,
        32 => ThirtyTwo,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Only the 3-name and 32-name corpora are supported."),
    };

    private static ProviderBenchmarkCorpus CreateThirtyTwo()
    {
        // Deliberately unsorted, including invalid names; selection must parse and filter the corpus.
        string[] names =
        [
            "3.0.4", "1.0.2", "4.0.0-beta.2", "2.0.7", "not-semver", "1.0.7", "3.0.0", "2.0.1",
            "4.0.0-alpha.1", "1.0.0", "3.0.7", "2.0.4", "", "4.0.0-rc.1", "1.0.4", "3.0.2",
            "2.0.0", "4.0.0-beta.1", "1.0.6", "3.0.5", "2.0.3", "4.0.0-rc.2", "1.0.1", "3.0.1",
            "2.0.6", "4.0.0-alpha.2", "1.0.5", "3.0.6", "2.0.2", "1.0.3", "3.0.3", "2.0.5",
        ];
        var nuGetPayload = "{\"versions\":[" + string.Join(',', names.Select(name => $"\"{name}\"")) + "]}";
        var gitHubPayload = "[" + string.Join(',', names.Select(name => $"{{\"name\":\"{name}\"}}")) + "]";
        return new ProviderBenchmarkCorpus(nuGetPayload, gitHubPayload, "3.0.7", "4.0.0-rc.2", "1.0.7");
    }
}

/// <summary>
/// Verifies real provider results outside measurement and materializes warm entries through actual
/// 304 revalidation. Range and prerelease checks do not add benchmark cases.
/// </summary>
internal static class ProviderBenchmarkPreflight
{
    public static async Task ValidateAsync(NuGetPackageService service, ProviderBenchmarkCorpus corpus, HttpStatusCode statusCode)
    {
        var stable = await service.GetLatestVersionAsync("benchmark.package").ConfigureAwait(false);
        Validate(stable, corpus.StableVersion, "stable", statusCode);
        var prerelease = await service.GetLatestVersionAsync("benchmark.package", includePrerelease: true).ConfigureAwait(false);
        Validate(prerelease, corpus.PrereleaseVersion, "prerelease", statusCode);
        if (corpus.RangeVersion is not null)
        {
            var range = await service.GetLatestVersionAsync("benchmark.package", "[1.0.0,2.0.0)").ConfigureAwait(false);
            Validate(range, corpus.RangeVersion, "range [1.0.0,2.0.0)", statusCode);
        }
    }

    public static async Task ValidateAsync(GitHubPackageService service, ProviderBenchmarkCorpus corpus, HttpStatusCode statusCode)
    {
        var stable = await service.GetLatestVersionAsync("benchmark-org", "benchmark.package", "token").ConfigureAwait(false);
        Validate(stable, corpus.StableVersion, "stable", statusCode);
        var prerelease = await service.GetLatestVersionAsync("benchmark-org", "benchmark.package", "token", includePrerelease: true).ConfigureAwait(false);
        Validate(prerelease, corpus.PrereleaseVersion, "prerelease", statusCode);
        if (corpus.RangeVersion is not null)
        {
            var range = await service.GetLatestVersionAsync("benchmark-org", "benchmark.package", "token", "[1.0.0,2.0.0)").ConfigureAwait(false);
            Validate(range, corpus.RangeVersion, "range [1.0.0,2.0.0)", statusCode);
        }
    }

    private static void Validate(NuGetResults result, string expectedVersion, string criterion, HttpStatusCode statusCode)
    {
        if (!result.IsSuccess || result.NuGetPackageInfo!.VersionString != expectedVersion)
        {
            var actualOutcome = result.IsSuccess ? $"success '{result.NuGetPackageInfo!.VersionString}'" : $"failure '{result.Failure}'";
            throw new InvalidOperationException($"NuGet/{(int)statusCode} {criterion} benchmark preflight expected '{expectedVersion}', actual {actualOutcome}.");
        }
    }

    private static void Validate(GitHubPackageResult result, string expectedVersion, string criterion, HttpStatusCode statusCode)
    {
        if (!result.IsSuccess || result.GitHubPackageInfo!.VersionString != expectedVersion)
        {
            var actualOutcome = result.IsSuccess ? $"success '{result.GitHubPackageInfo!.VersionString}'" : $"failure '{result.Failure}'";
            throw new InvalidOperationException($"GitHub/{(int)statusCode} {criterion} benchmark preflight expected '{expectedVersion}', actual {actualOutcome}.");
        }
    }
}
