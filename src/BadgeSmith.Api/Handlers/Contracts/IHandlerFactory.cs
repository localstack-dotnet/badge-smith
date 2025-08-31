namespace BadgeSmith.Api.Handlers.Contracts;

internal interface IHandlerFactory
{
    public IHealthCheckHandler HealthCheckHandler { get; }
    public INugetPackageBadgeHandler NugetPackageBadgeHandler { get; }
    public IGithubPackagesBadgeHandler GithubPackagesBadgeHandler { get; }
    public ITestResultsBadgeHandler TestResultsBadgeHandler { get; }
    public ITestResultRedirectionHandler TestResultRedirectionHandler { get; }
    public ITestResultIngestionHandler TestResultIngestionHandler { get; }
}
