#pragma warning disable S1075

using System.Net;

namespace BadgeSmith.Api.Infrastructure;

/// <summary>
/// Singleton HTTP stack optimized for Lambda execution with connection pooling
/// and service-specific configurations. Handlers live for the Lambda process lifetime.
/// </summary>
internal static class HttpClientFactory
{
    private const string NugetApiUrl = "https://api.nuget.org/";
    private const string GithubApiUrl = "https://api.github.com/";

    private static readonly Lazy<SocketsHttpHandler> NugetSocketsHttpHandlerFactory = new(CreateHandlerInstance());
    private static readonly Lazy<SocketsHttpHandler> GithubSocketsHttpHandlerFactory = new(CreateHandlerInstance());

    private static SocketsHttpHandler CreateHandlerInstance()
    {
        return new SocketsHttpHandler()
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
    }

    public static HttpClient CreateNuGetClient()
    {
        var httpClient = new HttpClient(NugetSocketsHttpHandlerFactory.Value, disposeHandler: false)
        {
            BaseAddress = new Uri(NugetApiUrl),
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("badge-smith/1.0 (+https://github.com/localstack-dotnet/badge-smith)");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        return httpClient;
    }

    public static HttpClient CreateGithubClient()
    {
        var httpClient = new HttpClient(GithubSocketsHttpHandlerFactory.Value, disposeHandler: false)
        {
            BaseAddress = new Uri(GithubApiUrl),
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("badge-smith/1.0 (+https://github.com/localstack-dotnet/badge-smith)");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

        return httpClient;
    }
}
