#pragma warning disable S1075

using System.Diagnostics;
using System.Net;
using BadgeSmith.Api.Observability.Loggers;

namespace BadgeSmith.Api.Infrastructure;

/// <summary>
/// Singleton HTTP stack optimized for Lambda execution with connection pooling
/// and service-specific configurations. Handlers live for the Lambda process lifetime.
/// </summary>
internal static class HttpStack
{
    private const string NugetApiUrl = "https://api.nuget.org/";
    // private const string GithubApiUrl = "https://api.github.com/";

    private static SocketsHttpHandler? _nuGetHandler;

    // private static readonly SocketsHttpHandler GitHubHandler = new()
    // {
    //     AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    //     PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    //     ConnectTimeout = TimeSpan.FromSeconds(2),
    //     UseProxy = false,
    //     MaxConnectionsPerServer = 5,
    // };

    /// <summary>
    /// HttpClient for NuGet.org API calls. Configured for the v3 flat container API.
    /// </summary>
    private static HttpClient? _nuGet;

    public static SocketsHttpHandler CreateNuGetHandler()
    {
        if (_nuGetHandler != null)
        {
            return _nuGetHandler;
        }

        var stopwatch = Stopwatch.StartNew();
        _nuGetHandler = new SocketsHttpHandler()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
            MaxConnectionsPerServer = 8,
            AllowAutoRedirect = false,
        };

        stopwatch.Stop();
        var ms = stopwatch.ElapsedMilliseconds;
        SimpleLogger.LogInformation(nameof(HttpStack), $"NuGet handler created in {ms} ms");

        return _nuGetHandler;
    }

    public static HttpClient CreateNuGetClient()
    {
        if (_nuGet != null)
        {
            return _nuGet;
        }

        var stopwatch = Stopwatch.StartNew();
#pragma warning disable CA2000
        var socketsHttpHandler = CreateNuGetHandler();
#pragma warning restore CA2000
        _nuGet = new HttpClient(socketsHttpHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(NugetApiUrl),
            Timeout = TimeSpan.FromSeconds(10), // NuGet can be slow sometimes
            DefaultRequestVersion = HttpVersion.Version30,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        stopwatch.Stop();
        var ms = stopwatch.ElapsedMilliseconds;
        SimpleLogger.LogInformation(nameof(HttpStack), $"NuGet HttpClient created in {ms} ms");

        _nuGet.DefaultRequestHeaders.UserAgent.ParseAdd("badge-smith/1.0 (+https://github.com/localstack-dotnet/badge-smith)");
        _nuGet.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        return _nuGet;
    }

    // /// <summary>
    // /// HttpClient for GitHub API calls. Future use for GitHub packages.
    // /// </summary>
    // public static readonly HttpClient GitHub = new(GitHubHandler, disposeHandler: false)
    // {
    //     BaseAddress = new Uri(GithubApiUrl),
    //     Timeout = TimeSpan.FromSeconds(5),
    // };

    static HttpStack()
    {
        // NuGet client configuration
        // _nuGet.DefaultRequestHeaders.UserAgent.ParseAdd("badge-smith/1.0 (+https://github.com/localstack-dotnet/badge-smith)");
        // _nuGet.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        // // GitHub client configuration
        // GitHub.DefaultRequestHeaders.UserAgent.ParseAdd("badge-smith/1.0 (+https://github.com/localstack-dotnet/badge-smith)");
        // GitHub.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
    }
}
