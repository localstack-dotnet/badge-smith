namespace BadgeSmith.Api.Infrastructure.Routing.Contracts;

internal interface IRouteResolver
{
    public bool TryResolve(string method, string path, out RouteMatch match);

    public IReadOnlyList<string> GetAllowedMethods(string path);
}
