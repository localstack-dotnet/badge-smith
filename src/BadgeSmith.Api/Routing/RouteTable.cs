using BadgeSmith.Api.Routing.Patterns;

namespace BadgeSmith.Api.Routing;

internal static class RouteTable
{
    public static RouteDescriptor[] Routes { get; set; } =
    [
        new RouteDescriptor(
            Name: "Health",
            Method: "GET",
            RequiresAuth: false,
            HandlerFactory: factory => factory.HealthCheckHandler,
            Pattern: new ExactPattern("/health")),

        new RouteDescriptor(
            Name: "NugetPackageBadge",
            Method: "GET",
            RequiresAuth: false,
            HandlerFactory: factory => factory.NugetPackageBadgeHandler,
            Pattern: new TemplatePattern("/badges/packages/{provider}/{package}")),

        new RouteDescriptor(
            Name: "GithubPackagesBadge",
            Method: "GET",
            RequiresAuth: false,
            HandlerFactory: factory => factory.GithubPackagesBadgeHandler,
            Pattern: new TemplatePattern("/badges/packages/{provider}/{org}/{package}")),

        new RouteDescriptor(
            Name: "TestsBadge",
            Method: "GET",
            RequiresAuth: false,
            HandlerFactory: factory => factory.TestResultsBadgeHandler,
            Pattern: new TemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}")),

        new RouteDescriptor(
            Name: "TestIngestion",
            Method: "POST",
            RequiresAuth: true,
            HandlerFactory: factory => factory.TestResultIngestionHandler,
            Pattern: new ExactPattern("/tests/results")),

        new RouteDescriptor(
            Name: "BadgeRedirect",
            Method: "GET",
            RequiresAuth: false,
            HandlerFactory: factory => factory.TestResultRedirectionHandler,
            Pattern: new TemplatePattern("/redirect/test-results/{platform}/{owner}/{repo}/{branch}")),
    ];
}
