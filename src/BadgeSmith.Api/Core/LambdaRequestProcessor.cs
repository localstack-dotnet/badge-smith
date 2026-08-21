#pragma warning disable CA1873 // Replace with LoggerMessage source-generated logging.

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Core.Routing.Contracts;
using BadgeSmith.Api.Core.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Core;

/// <summary>
/// The single production request pipeline: timeout budget, method/path fallbacks, request-ID logging
/// scope, router invocation, and the safe unhandled-error response.
/// </summary>
/// <param name="apiRouter">The application router invoked for every request.</param>
/// <param name="logger">The category logger created once at initialization.</param>
internal sealed class LambdaRequestProcessor(IApiRouter apiRouter, ILogger logger)
{
    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        using var cts = new CancellationTokenSource(Settings.LambdaTimeout);

        var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
        var path = request.RequestContext.Http.Path ?? "/";

        using var beginScope = logger.BeginScope(context.AwsRequestId);
        logger.LogInformation("Handling {Method} {Path}", httpMethod, path);

        try
        {
            return await apiRouter.RouteAsync(request, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error");
            return ResponseHelper.InternalServerError("An error occurred processing the request");
        }
    }
}

#pragma warning restore CA1873
