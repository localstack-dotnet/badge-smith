namespace BadgeSmith.Api.Core.Routing.Contracts;

internal interface IRoutePattern
{
    public bool TryMatch(ReadOnlySpan<char> path, ref RouteValues values);
}
