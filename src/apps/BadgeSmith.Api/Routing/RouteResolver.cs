using System.Collections.Frozen;
using BadgeSmith.Api.Routing.Contracts;
using ZLinq;

namespace BadgeSmith.Api.Routing;

/// <summary>
/// Provides efficient route matching and handler caching for AWS Lambda API Gateway requests.
/// Uses source-generated regex patterns and linear search optimization for minimal latency.
/// </summary>
internal class RouteResolver : IRouteResolver
{
    private readonly FrozenSet<RouteEntry> _routeEntries;

    public RouteResolver(FrozenSet<RouteEntry> routeEntries)
    {
        _routeEntries = routeEntries;
    }

    /// <summary>
    /// Resolves a route from the incoming HTTP request by matching the path and method against registered routes.
    /// Uses optimized linear search with early HTTP method filtering and source-generated regex matching.
    /// </summary>
    /// <param name="path">The request path to match against route patterns (e.g., "/badges/packages/nuget/MyPackage")</param>
    /// <param name="method">The HTTP method to match (GET, POST, PUT, DELETE, etc.)</param>
    /// <returns>A RouteEntry with populated Match data if found, or null if no route matches the request</returns>
    public RouteEntry? ResolveRoute(string path, string method)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(method);

        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(RouteResolver)}.{nameof(ResolveRoute)}");

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var normalizeMethod = NormalizeMethod(method);

        // Linear search through routes (6 routes max = negligible overhead)
        foreach (var route in _routeEntries)
        {
            // Check HTTP method first (fast string comparison)
            if (!route.Method.Equals(normalizeMethod, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = route.CompiledRegex.Match(path);
            if (match.Success)
            {
                return route with
                {
                    Match = match,
                };
            }
        }

        return null;
    }

    public IReadOnlyList<string> GetAllowedMethods(string path)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(RouteResolver)}.{nameof(GetAllowedMethods)}");

        ArgumentNullException.ThrowIfNull(path);

        var methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var matchingMethods = _routeEntries
            .AsValueEnumerable()
            .Where(route => route.CompiledRegex.IsMatch(path))
            .Select(route => route.Method);

        foreach (var method in matchingMethods)
        {
            methods.Add(method);
        }

        if (methods.Contains("GET"))
        {
            methods.Add("HEAD");
        }

        methods.Add("OPTIONS");
        return methods.Count == 0 ? [] : methods.AsValueEnumerable().Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizeMethod(string method) => method.Equals("HEAD", StringComparison.OrdinalIgnoreCase) ? "GET" : method;
}
