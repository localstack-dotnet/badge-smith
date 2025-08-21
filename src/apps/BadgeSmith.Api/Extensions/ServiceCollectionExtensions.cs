#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using System.Collections.Frozen;
using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;

namespace Microsoft.Extensions.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBadgeSmithApi(this IServiceCollection services)
    {
        services.AddKeyedSingleton("routeEntries", RouteTable.Routes.ToFrozenSet());

        services.AddTransient<IApiRouter, ApiRouter>();
        services.AddTransient<IRouteResolver, RouteResolver>();
        services.AddTransient<IHealthCheckHandler, HealthCheckHandler>();

        return services;
    }
}
