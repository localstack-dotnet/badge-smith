using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Patterns;

namespace BadgeSmith.Api.Routing;

internal sealed class RouteResolver : IRouteResolver
{
    private readonly RouteDescriptor[] _routes;

    public RouteResolver(RouteDescriptor[] allRoutes)
    {
        _routes = allRoutes;
    }

    public bool TryResolve(string method, string path, out RouteMatch match)
    {
        var norm = Normalize(method);
        var paramBuffer = new (string, int, int)[8]; // Shared buffer for all route checks

        // Iterate through routes in order - exact patterns first for performance
        foreach (var d in _routes)
        {
            if (!string.Equals(Normalize(d.Method), norm, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Handle exact pattern matching inline
            if (d.Pattern is ExactPattern ep)
            {
                if (string.Equals(ep.Literal, path, StringComparison.OrdinalIgnoreCase))
                {
                    // No parameters for exact matches
                    var buffer = Array.Empty<(string, int, int)>();
                    var values = new RouteValues(path.AsSpan(), buffer.AsSpan());
                    match = new RouteMatch(d, values);
                    return true;
                }

                continue;
            }

            // Handle pattern matching (templates, regex, etc.) - reuse shared buffer
            var routeValues = new RouteValues(path.AsSpan(), paramBuffer.AsSpan());
            if (d.Pattern.TryMatch(path.AsSpan(), ref routeValues))
            {
                match = new RouteMatch(d, routeValues);
                return true;
            }
        }

        match = default;
        return false;
    }

    public IReadOnlyList<string> GetAllowedMethods(string path)
    {
        var methods = new List<string>();
        var paramBuffer = new (string, int, int)[8];

        // Check all routes in order
        foreach (var d in _routes)
        {
            // Handle exact pattern matching inline
            if (d.Pattern is ExactPattern ep)
            {
                if (string.Equals(ep.Literal, path, StringComparison.OrdinalIgnoreCase))
                {
                    methods.Add(d.Method);
                }

                continue;
            }

            // Handle pattern matching
            var vals = new RouteValues(path.AsSpan(), paramBuffer.AsSpan());
            if (d.Pattern.TryMatch(path.AsSpan(), ref vals))
            {
                methods.Add(d.Method);
            }
        }

        // Add HEAD support for GET routes
        if (methods.Contains("GET", StringComparer.OrdinalIgnoreCase) && !methods.Contains("HEAD", StringComparer.OrdinalIgnoreCase))
        {
            methods.Add("HEAD");
        }

        // Always add OPTIONS for CORS
        if (!methods.Contains("OPTIONS", StringComparer.OrdinalIgnoreCase))
        {
            methods.Add("OPTIONS");
        }

        return methods;
    }

    private static string Normalize(string method) => method.Equals("HEAD", StringComparison.OrdinalIgnoreCase) ? "GET" : method;
}
