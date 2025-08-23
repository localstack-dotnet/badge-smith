#pragma warning disable CA1502

using System.Collections.Frozen;
using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

var routeResolver = new RouteResolver(RouteTable.Routes.ToFrozenSet());
var handlerFactory = new HandlerFactory();
var apiRouter = new ApiRouter(routeResolver, handlerFactory);

var _ = bool.TryParse(Environment.GetEnvironmentVariable("ObservabilityOptions:EnableOtel"), out var enableOtel);

var handler = BuildHandler(apiRouter, enableOtel);

await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>())
    .Build()
    .RunAsync()
    .ConfigureAwait(false);

return;

static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(ApiRouter apiRouter, bool enableOtel)
{
    var lazyProvider = new Lazy<TracerProvider?>(CreateTracerProvider);

    if (enableOtel && lazyProvider.Value is not null)
    {
        return (req, ctx) =>
            AWSLambdaWrapper.TraceAsync(
                lazyProvider.Value,
                (wrappedReq, wrappedCtx) => FunctionCoreAsync(wrappedReq, wrappedCtx, apiRouter),
                req, ctx);
    }

    return (req, ctx) => FunctionCoreAsync(req, ctx, apiRouter);
}

static async Task<APIGatewayHttpApiV2ProxyResponse> FunctionCoreAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context, ApiRouter apiRouter)
{
    var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
    var path = request.RequestContext.Http.Path ?? "/";
    var routeKey = request.RequestContext.RouteKey; // e.g., "GET /badges/{id}"
    var route = routeKey?.IndexOf(' ', StringComparison.OrdinalIgnoreCase) is { } idx and >= 0 ? routeKey[(idx + 1)..] : null;

    var span = Activity.Current;

    var name = $"{httpMethod} {route ?? path}";
    if (span is not null)
    {
        span.DisplayName = name;
        span.SetTag("http.request.method", httpMethod)
            .SetTag("url.path", path);

        if (!string.IsNullOrEmpty(route))
        {
            span.SetTag("http.route", route);
        }

        if (request.Headers?.TryGetValue("host", out var hostHeader) == true)
        {
            span.SetTag("server.address", hostHeader);
        }

        if (request.Headers?.TryGetValue("x-forwarded-proto", out var proto) == true)
        {
            span.SetTag("url.scheme", proto);
        }

        if (request.RequestContext?.Stage is { } stage)
        {
            span.SetTag("server.port", string.Equals(stage, "$default", StringComparison.OrdinalIgnoreCase) ? null : stage);
        }
    }

    context.Logger.LogInformation("Handling {Method} {Path}", httpMethod, path);

    APIGatewayHttpApiV2ProxyResponse? response = null;

    try
    {
        response = await apiRouter.RouteAsync(request, context).ConfigureAwait(false);
        return response;
    }
    catch (Exception ex)
    {
        span?.SetStatus(ActivityStatusCode.Error, ex.Message);
        span?.AddException(ex);
        context.Logger.LogError(ex, "Unhandled error");
        return ResponseHelper.InternalServerError("An error occurred processing the request");
    }
    finally
    {
        if (Activity.Current is { } s && response is not null)
        {
            s.SetTag("http.response.status_code", response.StatusCode);
        }
    }
}

static TracerProvider CreateTracerProvider()
{
    // var config = new ConfigurationBuilder()
    //     .AddEnvironmentVariables()
    //     .Build();

    var env = new EnvFromVariables();
    var services = new ServiceCollection();

    services.ConfigureOpenTelemetry(env, serviceName: "badge-smith-api", serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString());

    var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateScopes = false,
    });

    return serviceProvider.GetRequiredService<TracerProvider>();
}
