using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Core.Routing.Contracts;
using BadgeSmith.Api.Core.Routing.Helpers;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace BadgeSmith.Api.Core;

/// <summary>
/// The single production request pipeline: timeout budget, method/path fallbacks, request-ID logging
/// scope, router invocation, and the safe unhandled-error response.
/// </summary>
/// <param name="apiRouter">The application router invoked for every request.</param>
/// <param name="logger">The category logger created once at initialization.</param>
internal sealed partial class LambdaRequestProcessor(IApiRouter apiRouter, ILogger logger)
{
    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        using var cts = new CancellationTokenSource(Settings.LambdaTimeout);

        var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
        var path = request.RequestContext.Http.Path ?? "/";

        using var beginScope = logger.BeginScope(context.AwsRequestId);
        LogLambdaRequestHandling(logger, httpMethod, path);

        try
        {
            return await apiRouter.RouteAsync(request, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogLambdaUnhandledError(logger, ex);
            return ResponseHelper.InternalServerError("An error occurred processing the request");
        }
    }

    [LoggerMessage(EventId = 1001, EventName = "LambdaRequestHandling", Level = LogLevel.Information, Message = "Handling {Method} {Path}")]
    private static partial void LogLambdaRequestHandling(ILogger logger, string method, string path);

    [LoggerMessage(EventId = 1002, EventName = "LambdaUnhandledError", Level = LogLevel.Error, Message = "Unhandled error")]
    private static partial void LogLambdaUnhandledError(ILogger logger, Exception exception);
}
