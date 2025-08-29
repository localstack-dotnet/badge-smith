using System.Diagnostics;
using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Observability;
using BadgeSmith.Api.Observability.Loggers;
using BadgeSmith.Api.Routing.Cors;

namespace BadgeSmith.Api.Routing.Helpers;

internal static class ApiRouterBuilder
{
    public static ApiRouter BuildApiRouter()
    {
        var buildStart = Stopwatch.GetTimestamp();

        var logger = LoggerFactory.CreateLogger<ApiRouter>();
        var routeResolver = new RouteResolver(RouteTable.Routes);
        var handlerFactory = new HandlerFactory();

        var corsLogger = LoggerFactory.CreateLogger<CorsHandler>();
        var corsHandler = new CorsHandler(routeResolver, corsLogger, new CorsOptions
        {
            AllowCredentials = false,
            UseWildcardWhenNoCredentials = true,
            MaxAgeSeconds = 3600,
        });

        var apiRouter = new ApiRouter(logger, routeResolver, handlerFactory, corsHandler);

        SimplePerfLogger.Log("BuildApiRouter Complete", buildStart, "Program.cs");

        return apiRouter;
    }
}
