#if !ENABLE_TELEMETRY
#pragma warning disable CA1502

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Infrastructure;
using LoggerFactory = BadgeSmith.Api.Core.Observability.LoggerFactory;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using Microsoft.Extensions.Logging;

var apiRouter = ApplicationRegistry.ApiRouter;
var handler = BuildHandler(apiRouter);

var jsonSerializer = new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>();
var lambdaBootstrap = LambdaBootstrapBuilder.Create(handler, jsonSerializer).Build();

await lambdaBootstrap.RunAsync().ConfigureAwait(false);
return;

static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(ApiRouter apiRouter)
{
    return (req, ctx) => FunctionCoreAsync(req, ctx, apiRouter);
}

static async Task<APIGatewayHttpApiV2ProxyResponse> FunctionCoreAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context, ApiRouter apiRouter)
{
    using var cts = new CancellationTokenSource(Settings.LambdaTimeout);

    var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
    var path = request.RequestContext.Http.Path ?? "/";

    var logger = LoggerFactory.CreateLogger<Program>();
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
#endif
