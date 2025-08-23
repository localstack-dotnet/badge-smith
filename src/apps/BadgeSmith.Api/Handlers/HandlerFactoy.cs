using BadgeSmith.Api.Routing.Contracts;

namespace BadgeSmith.Api.Handlers;

internal interface IHandlerFactory
{
    public IRouteHandler? CreateHandler(Type handlerType);
}

internal class HandlerFactory : IHandlerFactory
{
    public IRouteHandler? CreateHandler(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        if (!typeof(IRouteHandler).IsAssignableFrom(handlerType))
        {
            throw new InvalidOperationException($"Handler type {handlerType} does not implement {nameof(IRouteHandler)}");
        }

        return handlerType switch
        {
            _ when handlerType.IsAssignableTo(typeof(IHealthCheckHandler)) => new HealthCheckHandler(),
            _ when handlerType.IsAssignableTo(typeof(INugetPackageBadgeHandler)) => new NugetPackageBadgeHandler(),
            _ when handlerType.IsAssignableTo(typeof(IGithubPackagesBadgeHandler)) => new GithubPackagesBadgeHandler(),
            _ when handlerType.IsAssignableTo(typeof(ITestResultsBadgeHandler)) => new TestResultsBadgeHandler(),
            _ when handlerType.IsAssignableTo(typeof(ITestResultRedirectionHandler)) => new TestResultRedirectionHandler(),
            _ when handlerType.IsAssignableTo(typeof(ITestResultIngestionHandler)) => new TestResultIngestionHandler(),
            _ => null,
        };
    }
}
