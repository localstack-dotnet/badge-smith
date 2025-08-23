using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Patterns;
using ZLinq;

namespace BadgeSmith.Api.Routing;

internal sealed class RouteResolverV2 : IRouteResolverV2
{
    private readonly Dictionary<(string method, string path), RouteDescriptor> _exact;
    private readonly RouteDescriptor[] _patterns;

    public RouteResolverV2(RouteDescriptor[] exactDescriptors, RouteDescriptor[] patternDescriptors)
    {
        _exact = new Dictionary<(string, string), RouteDescriptor>(exactDescriptors.Length);
        foreach (var d in exactDescriptors)
        {
            if (d.Pattern is ExactPattern ep)
            {
                _exact[(Normalize(d.Method), ep.Literal)] = d;
            }
            else
            {
                throw new InvalidOperationException($"Expected ExactPattern for exact descriptor but got {d.Pattern.GetType().Name}");
            }
        }

        _patterns = patternDescriptors;
    }

    public bool TryResolve(string method, string path, out RouteMatch match)
    {
        var norm = Normalize(method);
        if (_exact.TryGetValue((norm, path), out var desc))
        {
            // Use heap-allocated array for exact matches since no params expected
            var buffer = Array.Empty<(string, int, int)>();
            var values = new RouteValues(path.AsSpan(), buffer.AsSpan());
            match = new RouteMatch(desc, values);
            return true;
        }

        // Use heap-allocated array for pattern matching
        var paramBuffer = new (string, int, int)[8];
        var routeValues = new RouteValues(path.AsSpan(), paramBuffer.AsSpan());

        foreach (var d in _patterns)
        {
            if (!string.Equals(Normalize(d.Method), norm, StringComparison.Ordinal))
            {
                continue;
            }

            if (d.Pattern.TryMatch(path.AsSpan(), ref routeValues))
            {
                match = new RouteMatch(d, routeValues);
                return true;
            }

            // Reset for next pattern
            routeValues = new RouteValues(path.AsSpan(), paramBuffer.AsSpan());
        }

        match = default;
        return false;
    }

    public IReadOnlyList<string> GetAllowedMethods(string path)
    {
        var methods = new List<string>();

        var foundMethods = _exact
            .AsValueEnumerable()
            .Where(kv => string.Equals(kv.Key.path, path, StringComparison.Ordinal))
            .Select(kv => kv.Value.Method);

        methods.AddRange(foundMethods.ToArray());

        // Check pattern matches
        foreach (var d in _patterns)
        {
            var paramBuffer = new (string, int, int)[8];
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
