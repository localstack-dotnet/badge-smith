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
/// Timing and allocation of one provider call using fake HTTP and a dictionary cache with 3 or 32 names.
/// Each 200 operation includes a response-cache reset; 304 revalidates a seeded, provider-primed entry.
/// ColdCache means response-cache cold after process/setup warming, not cold JIT or Lambda INIT.
/// Recorded evidence only; not a CI gate.
/// </summary>
[MemoryDiagnoser]
public class ProviderBenchmarks : IDisposable
{
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

    [Params(3, 32)]
    public int CorpusSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var corpus = ProviderBenchmarkCorpus.ForSize(CorpusSize);
        var nuGetVersionService = new NuGetVersionService();

        var nuGetColdHandler = Own(new ScriptedUpstreamHandler(HttpStatusCode.OK, corpus.NuGetPayload));
        var nuGetColdClient = new HttpClient(nuGetColdHandler, disposeHandler: false)
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
        var nuGetWarmClient = new HttpClient(nuGetWarmHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(NuGetBaseAddress),
        };
        var nuGetWarmCache = new ScriptedCache();
        nuGetWarmCache.Seed(NuGetCacheKey, new UpstreamCacheEntry(corpus.NuGetPayload, WeakEtag, null));
        _nuGetWarmService = new NuGetPackageService(
            nuGetVersionService,
            NullLogger<NuGetPackageService>.Instance,
            Own(nuGetWarmClient),
            nuGetWarmCache);

        var gitHubColdHandler = Own(new ScriptedUpstreamHandler(HttpStatusCode.OK, corpus.GitHubPayload));
        var gitHubColdClient = new HttpClient(gitHubColdHandler, disposeHandler: false)
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
        var gitHubWarmClient = new HttpClient(gitHubWarmHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(GitHubBaseAddress),
        };
        var gitHubWarmCache = new ScriptedCache();
        gitHubWarmCache.Seed(GitHubCacheKey, new UpstreamCacheEntry(corpus.GitHubPayload, WeakEtag, null));
        _gitHubWarmService = new GitHubPackageService(
            Own(gitHubWarmClient),
            nuGetVersionService,
            gitHubWarmCache,
            NullLogger<GitHubPackageService>.Instance);

        try
        {
            await ProviderBenchmarkPreflight.ValidateAsync(_nuGetColdService, corpus, HttpStatusCode.OK).ConfigureAwait(false);
            await ProviderBenchmarkPreflight.ValidateAsync(_nuGetWarmService, corpus, HttpStatusCode.NotModified).ConfigureAwait(false);
            await ProviderBenchmarkPreflight.ValidateAsync(_gitHubColdService, corpus, HttpStatusCode.OK).ConfigureAwait(false);
            await ProviderBenchmarkPreflight.ValidateAsync(_gitHubWarmService, corpus, HttpStatusCode.NotModified).ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    [Benchmark]
    public Task NuGet_ColdCache_FullFetch_200()
    {
        _nuGetColdCache.Reset();
        return _nuGetColdService.GetLatestVersionAsync("benchmark.package");
    }

    [Benchmark]
    public Task NuGet_WarmCache_Revalidate_304() =>
        _nuGetWarmService.GetLatestVersionAsync("benchmark.package");

    [Benchmark]
    public Task GitHub_ColdCache_FullFetch_200()
    {
        _gitHubColdCache.Reset();
        return _gitHubColdService.GetLatestVersionAsync("benchmark-org", "benchmark.package", "token");
    }

    [Benchmark]
    public Task GitHub_WarmCache_Revalidate_304() =>
        _gitHubWarmService.GetLatestVersionAsync("benchmark-org", "benchmark.package", "token");

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

        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }

        _disposables.Clear();
    }

    private T Own<T>(T disposable) where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }
}

#pragma warning restore CA1812, CA1852, CA1515
