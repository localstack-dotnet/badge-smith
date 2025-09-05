using BadgeSmith.Api.Infrastructure.Routing.Patterns;

namespace BadgeSmith.Api.Infrastructure.Routing;

internal static class RouteTable
{
    public static RouteDescriptor[] Routes { get; set; } =
    [
        new RouteDescriptor(
            Name: "Health",
            Method: "GET",
            HandlerFactory: factory => factory.HealthCheckHandler,
            Pattern: new ExactPattern("/health")),

        new RouteDescriptor(
            Name: "NugetPackageBadge",
            Method: "GET",
            HandlerFactory: factory => factory.NugetPackageBadgeHandler,
            Pattern: new TemplatePattern("/badges/packages/{provider}/{package}")),

        new RouteDescriptor(
            Name: "GithubPackagesBadge",
            Method: "GET",
            HandlerFactory: factory => factory.GithubPackagesBadgeHandler,
            Pattern: new TemplatePattern("/badges/packages/{provider}/{org}/{package}")),

        new RouteDescriptor(
            Name: "TestsBadge",
            Method: "GET",
            HandlerFactory: factory => factory.TestResultsBadgeHandler,
            Pattern: new TemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}")),

        new RouteDescriptor(
            Name: "TestIngestion",
            Method: "POST",
            HandlerFactory: factory => factory.TestResultIngestionHandler,
            Pattern: new TemplatePattern("/tests/results/{platform}/{owner}/{repo}/{branch}")),

        new RouteDescriptor(
            Name: "BadgeRedirect",
            Method: "GET",
            HandlerFactory: factory => factory.TestResultRedirectionHandler,
            Pattern: new TemplatePattern("/redirect/test-results/{platform}/{owner}/{repo}/{branch}")),
    ];
}
