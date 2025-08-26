namespace BadgeSmith.Api.Routing;

internal sealed class RouteContextSnapshot
{
    public string Method { get; }
    public string Path { get; }
    public RouteDescriptor Descriptor { get; }

    public IReadOnlyDictionary<string, string> RouteValues { get; }

    private RouteContextSnapshot(string method, string path, RouteDescriptor descriptor, IReadOnlyDictionary<string, string> routeValues)
    {
        Method = method;
        Path = path;
        Descriptor = descriptor;
        RouteValues = routeValues;
    }

    public bool TryGet(string name, out string? value) => RouteValues.TryGetValue(name, out value);

    public static RouteContextSnapshot FromValues(string method, string path, RouteDescriptor descriptor, RouteValues values)
    {
        var immutableDict = values.ToImmutableDictionary();
        return new RouteContextSnapshot(method, path, descriptor, immutableDict);
    }

    public static RouteContextSnapshot FromMatch(string method, string path, RouteMatch match)
    {
        var immutableDict = match.Values.ToImmutableDictionary();
        return new RouteContextSnapshot(method, path, match.Descriptor, immutableDict);
    }
}
