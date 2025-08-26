#if !ENABLE_TELEMETRY
#pragma warning disable CA1502

using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Observability.Loggers;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;
using LoggerFactory = BadgeSmith.Api.Observability.LoggerFactory;

var appStart = Stopwatch.GetTimestamp();

var logger = LoggerFactory.CreateLogger<ApiRouter>();

var routeResolver = new RouteResolver(RouteTable.Routes);
var handlerFactory = new HandlerFactory();
var apiRouter = new ApiRouter(logger, routeResolver, handlerFactory);

var jsonSerializer = new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>();
var lambdaBootstrap = LambdaBootstrapBuilder
    .Create<APIGatewayHttpApiV2ProxyRequest>((req, ctx) => FunctionCoreAsync(req, ctx, apiRouter), jsonSerializer).Build();

SimplePerfLogger.Log("Lambda Initialization Complete", appStart, "Program.cs");

await lambdaBootstrap.RunAsync().ConfigureAwait(false);
return;

static async Task<APIGatewayHttpApiV2ProxyResponse> FunctionCoreAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context, ApiRouter apiRouter)
{
    var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
    var path = request.RequestContext.Http.Path ?? "/";

    var logger = LoggerFactory.CreateLogger<Program>();
    using var beginScope = logger.BeginScope(context.AwsRequestId);
    logger.LogInformation("Handling {Method} {Path}", httpMethod, path);

    try
    {
        return await apiRouter.RouteAsync(path, httpMethod, request.Headers).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled error");
        return ResponseHelper.InternalServerError("An error occurred processing the request");
    }
}
#endif
