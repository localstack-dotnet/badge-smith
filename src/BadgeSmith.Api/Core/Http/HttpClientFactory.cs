using System.Net;
using static BadgeSmith.Constants;

namespace BadgeSmith.Api.Core.Http;

/// <summary>
/// Singleton HTTP stack optimized for Lambda execution with connection pooling
/// and service-specific configurations. Handlers live for the Lambda process lifetime.
/// </summary>
internal static class HttpClientFactory
{
#pragma warning disable S1075 // These are intentional public service defaults, overridable for local tests.
    private const string NugetApiUrl = "https://api.nuget.org/";
    private const string GithubApiUrl = "https://api.github.com/";
#pragma warning restore S1075

    private static Uri ResolveBaseUri(string envVar, string fallback)
    {
        var upstreamMode = ResolveUpstreamMode();
        var value = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (upstreamMode == UpstreamModeMock)
            {
                throw new InvalidOperationException($"{envVar} is required when {UpstreamModeEnvironmentVariable} is {UpstreamModeMock}.");
            }

            return new Uri(fallback);
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{envVar} must be an absolute HTTP or HTTPS URL without credentials, query, or fragment.");
        }

        if (upstreamMode == UpstreamModeLive && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{envVar} must use HTTPS when {UpstreamModeEnvironmentVariable} is {UpstreamModeLive}.");
        }

        // Base addresses must end with '/' for correct relative URI resolution
        // (e.g. HttpClient appends "v3-flatcontainer/..." relative to the base path).
        if (!uri.AbsolutePath.EndsWith('/'))
        {
            var builder = new UriBuilder(uri);
            builder.Path += "/";
            return builder.Uri;
        }

        return uri;
    }

    private static string ResolveUpstreamMode()
    {
        var value = Environment.GetEnvironmentVariable(UpstreamModeEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value) || value.Equals(UpstreamModeLive, StringComparison.OrdinalIgnoreCase))
        {
            return UpstreamModeLive;
        }

        if (value.Equals(UpstreamModeMock, StringComparison.OrdinalIgnoreCase))
        {
#if ENABLE_LOCALSTACK
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTP_NUGET_BASE_URL"))
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTP_GITHUB_BASE_URL")))
            {
                throw new InvalidOperationException(
                    $"{UpstreamModeMock} upstream mode requires both HTTP_NUGET_BASE_URL and HTTP_GITHUB_BASE_URL.");
            }

            return UpstreamModeMock;
#else
            throw new InvalidOperationException($"{UpstreamModeMock} upstream mode is unavailable in production builds.");
#endif
        }

        throw new InvalidOperationException(
            $"{UpstreamModeEnvironmentVariable} must be either {UpstreamModeLive} or {UpstreamModeMock}.");
    }

    private static readonly Lazy<SocketsHttpHandler> NugetSocketsHttpHandlerFactory = new(CreateHandlerInstance());
    private static readonly Lazy<SocketsHttpHandler> GithubSocketsHttpHandlerFactory = new(CreateHandlerInstance());
    private static readonly Lazy<HttpMessageHandler> NugetRetryHandlerFactory = new(() => new ResilienceRetryHandler(NugetSocketsHttpHandlerFactory.Value));
    private static readonly Lazy<HttpMessageHandler> GithubRetryHandlerFactory = new(() => new ResilienceRetryHandler(GithubSocketsHttpHandlerFactory.Value));

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
        var httpClient = new HttpClient(NugetRetryHandlerFactory.Value, disposeHandler: false)
        {
            BaseAddress = ResolveBaseUri("HTTP_NUGET_BASE_URL", NugetApiUrl),
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("badge-smith/1.0 (+https://github.com/localstack-dotnet/badge-smith)");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        return httpClient;
    }

    public static HttpClient CreateGithubClient()
    {
        var httpClient = new HttpClient(GithubRetryHandlerFactory.Value, disposeHandler: false)
        {
            BaseAddress = ResolveBaseUri("HTTP_GITHUB_BASE_URL", GithubApiUrl),
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("badge-smith/1.0 (+https://github.com/localstack-dotnet/badge-smith)");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

        return httpClient;
    }
}
