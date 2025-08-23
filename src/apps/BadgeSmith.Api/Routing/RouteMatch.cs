namespace BadgeSmith.Api.Routing;

internal readonly ref struct RouteMatch(RouteDescriptor descriptor, RouteValues values)
{
    public RouteDescriptor Descriptor { get; } = descriptor;
    public RouteValues Values { get; } = values;
}
