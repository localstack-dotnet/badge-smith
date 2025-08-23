using BadgeSmith.Api.Routing.Contracts;

namespace BadgeSmith.Api.Handlers;

/// <summary>
/// Pre-initialized handler registry for zero-allocation handler resolution.
/// All handlers are created during application startup to eliminate cold start overhead.
/// </summary>
internal static class HandlerRegistry
{
    private static readonly Dictionary<Type, IRouteHandler> Handlers = new()
    {
        [typeof(IHealthCheckHandler)] = new HealthCheckHandler(),
        [typeof(INugetPackageBadgeHandler)] = new NugetPackageBadgeHandler(),
        [typeof(IGithubPackagesBadgeHandler)] = new GithubPackagesBadgeHandler(),
        [typeof(ITestResultsBadgeHandler)] = new TestResultsBadgeHandler(),
        [typeof(ITestResultRedirectionHandler)] = new TestResultRedirectionHandler(),
        [typeof(ITestResultIngestionHandler)] = new TestResultIngestionHandler(),
    };

    /// <summary>
    /// Gets a pre-initialized handler for the specified type.
    /// O(1) dictionary lookup with no allocations.
    /// </summary>
    /// <param name="handlerType">The handler interface type to resolve.</param>
    /// <returns>The handler instance, or null if not found.</returns>
    public static IRouteHandler? GetHandler(Type handlerType) =>
        Handlers.GetValueOrDefault(handlerType);

    /// <summary>
    /// Gets the total number of registered handlers.
    /// </summary>
    public static int Count => Handlers.Count;
}

