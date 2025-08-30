using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Observability;
using BadgeSmith.Api.Routing.Cors;

namespace BadgeSmith.Api.Routing.Helpers;

internal static class ApiRouterBuilder
{
    public static ApiRouter BuildApiRouter()
    {
        //using var scope = PerfTracker.StartScope("BuildApiRouter Complete", nameof(ApiRouterBuilder));

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

        return new ApiRouter(logger, routeResolver, handlerFactory, corsHandler);
    }
}
