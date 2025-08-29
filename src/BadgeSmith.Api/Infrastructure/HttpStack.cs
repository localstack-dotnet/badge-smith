#pragma warning disable S1075

using System.Net;

namespace BadgeSmith.Api.Infrastructure;

/// <summary>
/// Singleton HTTP stack optimized for Lambda execution with connection pooling
/// and service-specific configurations. Handlers live for the Lambda process lifetime.
/// </summary>
internal static class HttpStack
{
    private const string NugetApiUrl = "https://api.nuget.org/";
    private const string GithubApiUrl = "https://api.github.com/";

    private static readonly SocketsHttpHandler NuGetHandler = new()
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(2),
        UseProxy = false,
        MaxConnectionsPerServer = 10,
    };

    private static readonly SocketsHttpHandler GitHubHandler = new()
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(2),
        UseProxy = false,
        MaxConnectionsPerServer = 5,
    };

    /// <summary>
    /// HttpClient for NuGet.org API calls. Configured for the v3 flat container API.
    /// </summary>
    public static readonly HttpClient NuGet = new(NuGetHandler, disposeHandler: false)
    {
        BaseAddress = new Uri(NugetApiUrl),
        Timeout = TimeSpan.FromSeconds(10), // NuGet can be slow sometimes
    };

    /// <summary>
    /// HttpClient for GitHub API calls. Future use for GitHub packages.
    /// </summary>
    public static readonly HttpClient GitHub = new(GitHubHandler, disposeHandler: false)
    {
        BaseAddress = new Uri(GithubApiUrl),
        Timeout = TimeSpan.FromSeconds(5),
    };

    static HttpStack()
    {
        // NuGet client configuration
        NuGet.DefaultRequestHeaders.UserAgent.ParseAdd("badge-smith/1.0 (+https://github.com/localstack-dotnet/badge-smith)");
        NuGet.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        // GitHub client configuration
        GitHub.DefaultRequestHeaders.UserAgent.ParseAdd("badge-smith/1.0 (+https://github.com/localstack-dotnet/badge-smith)");
        GitHub.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
    }
}
