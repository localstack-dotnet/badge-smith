using System.Collections.Frozen;
using Amazon.Lambda.APIGatewayEvents;
using JetBrains.Annotations;
using Serilog.Context;

namespace BadgeSmith.Api.Extensions;

internal static class ApiGatewayLoggingExtensions
{
    /// <summary>
    /// Enrich Serilog logs with useful AWS API Gateway request context properties.
    /// Call at the beginning of request handling.
    /// </summary>
    /// <param name="request">The API Gateway HTTP API v2 proxy request containing the full request information.</param>
    /// <param name="includeHeaders">Whether to include HTTP headers in the log context.</param>
    /// <param name="redactSensitiveHeaders">Whether to redact sensitive headers like authorization and cookies.</param>
    /// <param name="maxHeaderCount">Maximum number of headers to include before truncating.</param>
    /// <param name="maxHeaderValueLength">Maximum length of header values before truncating.</param>
    /// <returns>An IDisposable that removes the logged properties when disposed of.</returns>
    public static IDisposable PushApiGatewayContext(
        this APIGatewayHttpApiV2ProxyRequest request,
        bool includeHeaders = true,
        bool redactSensitiveHeaders = true,
        int maxHeaderCount = 50,
        int maxHeaderValueLength = 256)
    {
        var disposables = new List<IDisposable>();

        var ctx = request.RequestContext;

        FrozenDictionary<string, object?>? headers = null;
        if (includeHeaders && request.Headers is not null)
        {
            headers = SanitizeHeaders(request.Headers, redactSensitiveHeaders, maxHeaderCount, maxHeaderValueLength);
        }

        var proxyRequestContextLog = new ProxyRequestContextLog(
            ApiId: ctx.ApiId,
            AccountId: ctx.AccountId,
            DomainName: ctx.DomainName,
            DomainPrefix: ctx.DomainPrefix,
            RequestId: ctx.RequestId,
            RouteKey: ctx.RouteKey,
            Stage: ctx.Stage,
            Time: ctx.Time,
            TimeEpoch: ctx.TimeEpoch,
            Http: ctx.Http is null
                ? null
                : new HttpInfo(
                    ctx.Http.Method,
                    ctx.Http.Path,
                    ctx.Http.Protocol,
                    ctx.Http.SourceIp,
                    ctx.Http.UserAgent
                ),
            Headers: headers ?? FrozenDictionary<string, object?>.Empty
        );

        disposables.Add(LogContext.PushProperty("ProxyRequestContext", proxyRequestContextLog, destructureObjects: true));

        return new DisposableCollection(disposables);
    }

    /// <summary>
    /// Enrich Serilog logs with useful AWS API Gateway request context properties.
    /// This overload is for when you only have the request context without headers.
    /// </summary>
    /// <param name="context">The API Gateway request context containing request metadata and properties to log.</param>
    /// <returns>An IDisposable that removes the logged properties when disposed of.</returns>
    public static IDisposable PushApiGatewayContext(this APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext? context)
    {
        var disposables = new List<IDisposable>();

        if (context is null)
        {
            return new DisposableCollection(disposables);
        }

        var proxyRequestContext = new ProxyRequestContextLog(
            ApiId: context.ApiId,
            AccountId: context.AccountId,
            DomainName: context.DomainName,
            DomainPrefix: context.DomainPrefix,
            RequestId: context.RequestId,
            RouteKey: context.RouteKey,
            Stage: context.Stage,
            Time: context.Time,
            TimeEpoch: context.TimeEpoch,
            Http: context.Http is null
                ? null
                : new HttpInfo(
                    context.Http.Method,
                    context.Http.Path,
                    context.Http.Protocol,
                    context.Http.SourceIp,
                    context.Http.UserAgent
                ),
            Headers: FrozenDictionary<string, object?>.Empty
        );

        disposables.Add(LogContext.PushProperty("ProxyRequestContext", proxyRequestContext, destructureObjects: true));

        return new DisposableCollection(disposables);
    }

    [UsedImplicitly]
    private sealed record HttpInfo(string? Method, string? Path, string? Protocol, string? SourceIp, string? UserAgent);

    [UsedImplicitly]
    private sealed record ProxyRequestContextLog(
        string? ApiId,
        string? AccountId,
        string? DomainName,
        string? DomainPrefix,
        string? RequestId,
        string? RouteKey,
        string? Stage,
        string? Time,
        long? TimeEpoch,
        HttpInfo? Http,
        FrozenDictionary<string, object?> Headers
    );

    private static FrozenDictionary<string, object?> SanitizeHeaders(IDictionary<string, string> headers, bool redactSensitive, int maxHeaderCount, int maxValueLength)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var count = 0;
        foreach (var (key, value) in headers)
        {
            if (count >= maxHeaderCount)
            {
                result["__headersTruncated"] = true;
                break;
            }

            if (redactSensitive && IsSensitive(key))
            {
                result[key] = "***REDACTED***";
            }
            else if (value.Length > maxValueLength)
            {
                result[key] = $"{value.AsSpan(0, maxValueLength)}…";
                result[$"{key}__truncated"] = true;
            }
            else
            {
                result[key] = value;
            }

            count++;
        }

        return result.ToFrozenDictionary();

        // Redaction set
        static bool IsSensitive(string key) =>
            key.Equals("authorization", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("cookie", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("set-cookie", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("x-api-key", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("proxy-authorization", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DisposableCollection(IEnumerable<IDisposable> disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var d in disposables)
            {
                d.Dispose();
            }
        }
    }
}
