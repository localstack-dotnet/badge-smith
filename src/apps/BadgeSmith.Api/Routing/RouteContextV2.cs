namespace BadgeSmith.Api.Routing;

internal readonly ref struct RouteContextV2
{
    public RouteContextV2(string method, string path, RouteMatch match)
    {
        Method = method;
        Path = path;
        Values = match.Values;
        Descriptor = match.Descriptor;
    }

    public string Method { get; }

    public string Path { get; }

    public RouteValues Values { get; }

    public RouteDescriptor Descriptor { get; }

    public bool TryGetRouteValue(string name, out string value) => Values.TryGetString(name, out value);
    public bool TryGetRouteSpan(string name, out ReadOnlySpan<char> span) => Values.TryGetSpan(name, out span);
}
