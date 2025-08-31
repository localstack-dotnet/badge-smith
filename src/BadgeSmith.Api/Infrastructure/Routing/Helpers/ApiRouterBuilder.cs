using BadgeSmith.Api.Infrastructure.Handlers;
using BadgeSmith.Api.Infrastructure.Observability;
using BadgeSmith.Api.Infrastructure.Routing.Cors;

namespace BadgeSmith.Api.Infrastructure.Routing.Helpers;

internal static class ApiRouterBuilder
{
    public static ApiRouter BuildApiRouter()
    {
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
