namespace BadgeSmith.Api.Routing.Contracts;

internal interface IRouteResolverV2
{
    public bool TryResolve(string method, string path, out RouteMatch match);

    public IReadOnlyList<string> GetAllowedMethods(string path);
}
