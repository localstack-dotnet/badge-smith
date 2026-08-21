#pragma warning disable CA1812, CA1852, CA1515 // BenchmarkDotNet requires public, non-sealed types instantiated by generated code.

using System.Net;
using System.Net.Http.Headers;
using BadgeSmith.Api.Core.Http;
using BadgeSmith.Api.Core.Versioning;
using BadgeSmith.Api.Features.GitHub;
using BadgeSmith.Api.Features.NuGet;
using BadgeSmith.Api.Performance.Tests.TestHelpers;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;

namespace BadgeSmith.Api.Performance.Tests;

/// <summary>
/// Allocation profile of the provider upstream fetch: cold cache exercises the 200 fetch/parse/cache
/// path, warm cache exercises the 304 validator-replay path. Recorded evidence only; not a CI gate.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class ProviderBenchmarks : IDisposable
{
    private const string NugetIndexPayload = """{"versions":["1.0.0","1.2.0","2.0.0-preview"]}""";
    private const string GitHubVersionsPayload = """[{"name":"1.0.0"},{"name":"1.2.0"},{"name":"2.0.0-preview"}]""";
    private const string WeakEtag = "W/\"bench\"";
    private const string EtagTag = "\"bench\"";
    private const string NuGetCacheKey = "nuget:index:benchmark.package";
    private const string GitHubCacheKey = "github_package:index:benchmark-org:benchmark.package";
    private const string NuGetBaseAddress = "https://api.nuget.org/";
    private const string GitHubBaseAddress = "https://api.github.com/";

    private readonly List<IDisposable> _disposables = [];

    private ScriptedCache _nuGetColdCache = null!;
    private ScriptedCache _gitHubColdCache = null!;
    private NuGetPackageService _nuGetColdService = null!;
    private NuGetPackageService _nuGetWarmService = null!;
    private GitHubPackageService _gitHubColdService = null!;
    private GitHubPackageService _gitHubWarmService = null!;

    [GlobalSetup]
    public void Setup()
    {
        var nuGetVersionService = new NuGetVersionService();

        var nuGetColdHandler = Own(new ScriptedUpstreamHandler(HttpStatusCode.OK, NugetIndexPayload));
        var nuGetColdClient = new HttpClient(nuGetColdHandler)
        {
            BaseAddress = new Uri(NuGetBaseAddress),
        };
        _nuGetColdCache = new ScriptedCache();
        _nuGetColdService = new NuGetPackageService(
            nuGetVersionService,
            NullLogger<NuGetPackageService>.Instance,
            Own(nuGetColdClient),
            _nuGetColdCache);

        var nuGetWarmHandler = Own(new ScriptedUpstreamHandler(
            HttpStatusCode.NotModified, string.Empty, new EntityTagHeaderValue(EtagTag, isWeak: true)));
        var nuGetWarmClient = new HttpClient(nuGetWarmHandler)
        {
            BaseAddress = new Uri(NuGetBaseAddress),
        };
        var nuGetWarmCache = new ScriptedCache();
        nuGetWarmCache.Seed(NuGetCacheKey, new UpstreamCacheEntry(NugetIndexPayload, WeakEtag, null));
        _nuGetWarmService = new NuGetPackageService(
            nuGetVersionService,
            NullLogger<NuGetPackageService>.Instance,
            Own(nuGetWarmClient),
            nuGetWarmCache);

        var gitHubColdHandler = Own(new ScriptedUpstreamHandler(HttpStatusCode.OK, GitHubVersionsPayload));
        var gitHubColdClient = new HttpClient(gitHubColdHandler)
        {
            BaseAddress = new Uri(GitHubBaseAddress),
        };
        _gitHubColdCache = new ScriptedCache();
        _gitHubColdService = new GitHubPackageService(
            Own(gitHubColdClient),
            nuGetVersionService,
            _gitHubColdCache,
            NullLogger<GitHubPackageService>.Instance);

        var gitHubWarmHandler = Own(new ScriptedUpstreamHandler(
            HttpStatusCode.NotModified, string.Empty, new EntityTagHeaderValue(EtagTag, isWeak: true)));
        var gitHubWarmClient = new HttpClient(gitHubWarmHandler)
        {
            BaseAddress = new Uri(GitHubBaseAddress),
        };
        var gitHubWarmCache = new ScriptedCache();
        gitHubWarmCache.Seed(GitHubCacheKey, new UpstreamCacheEntry(GitHubVersionsPayload, WeakEtag, null));
        _gitHubWarmService = new GitHubPackageService(
            Own(gitHubWarmClient),
            nuGetVersionService,
            gitHubWarmCache,
            NullLogger<GitHubPackageService>.Instance);
    }

    [Benchmark]
    public async Task NuGet_ColdCache_FullFetch_200()
    {
        _nuGetColdCache.Reset();
        _ = await _nuGetColdService.GetLatestVersionAsync("benchmark.package").ConfigureAwait(false);
    }

    [Benchmark]
    public async Task NuGet_WarmCache_Revalidate_304() =>
        _ = await _nuGetWarmService.GetLatestVersionAsync("benchmark.package").ConfigureAwait(false);

    [Benchmark]
    public async Task GitHub_ColdCache_FullFetch_200()
    {
        _gitHubColdCache.Reset();
        _ = await _gitHubColdService.GetLatestVersionAsync("benchmark-org", "benchmark.package", "token").ConfigureAwait(false);
    }

    [Benchmark]
    public async Task GitHub_WarmCache_Revalidate_304() =>
        _ = await _gitHubWarmService.GetLatestVersionAsync("benchmark-org", "benchmark.package", "token").ConfigureAwait(false);

    [GlobalCleanup]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private T Own<T>(T disposable) where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }
}

#pragma warning restore CA1812, CA1852, CA1515
