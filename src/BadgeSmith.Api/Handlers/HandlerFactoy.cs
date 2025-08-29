#pragma warning disable S125, RCS1093

using BadgeSmith.Api.Domain.Services.Nuget;
using BadgeSmith.Api.Observability;

namespace BadgeSmith.Api.Handlers;

internal interface IHandlerFactory
{
    public IHealthCheckHandler HealthCheckHandler { get; }
    public INugetPackageBadgeHandler NugetPackageBadgeHandler { get; }
    public IGithubPackagesBadgeHandler GithubPackagesBadgeHandler { get; }
    public ITestResultsBadgeHandler TestResultsBadgeHandler { get; }
    public ITestResultRedirectionHandler TestResultRedirectionHandler { get; }
    public ITestResultIngestionHandler TestResultIngestionHandler { get; }
}

internal class HandlerFactory : IHandlerFactory
{
    private static readonly Lazy<IHealthCheckHandler> HealthCheckHandlerLazy = new(CreateHealthCheckHandler);
    private static readonly Lazy<INugetPackageBadgeHandler> NugetPackageBadgeHandlerLazy = new(CreateNugetPackageBadgeHandler);
    private static readonly Lazy<IGithubPackagesBadgeHandler> GithubPackagesBadgeHandlerLazy = new(CreateGithubPackagesBadgeHandler);
    private static readonly Lazy<ITestResultsBadgeHandler> TestResultsBadgeHandlerLazy = new(CreateTestResultsBadgeHandler);
    private static readonly Lazy<ITestResultRedirectionHandler> TestResultRedirectionHandlerLazy = new(CreateTestResultRedirectionHandler);
    private static readonly Lazy<ITestResultIngestionHandler> TestResultIngestionHandlerLazy = new(CreateTestResultIngestionHandler);

    public IHealthCheckHandler HealthCheckHandler => HealthCheckHandlerLazy.Value;

    public INugetPackageBadgeHandler NugetPackageBadgeHandler => NugetPackageBadgeHandlerLazy.Value;

    public IGithubPackagesBadgeHandler GithubPackagesBadgeHandler => GithubPackagesBadgeHandlerLazy.Value;

    public ITestResultsBadgeHandler TestResultsBadgeHandler => TestResultsBadgeHandlerLazy.Value;

    public ITestResultRedirectionHandler TestResultRedirectionHandler => TestResultRedirectionHandlerLazy.Value;

    public ITestResultIngestionHandler TestResultIngestionHandler => TestResultIngestionHandlerLazy.Value;

    private static HealthCheckHandler CreateHealthCheckHandler()
    {
        var logger = LoggerFactory.CreateLogger<HealthCheckHandler>();
        return new HealthCheckHandler(logger);
    }

    private static NugetPackageBadgeHandler CreateNugetPackageBadgeHandler()
    {
        var logger = LoggerFactory.CreateLogger<NugetPackageBadgeHandler>();
        var nugetPackageServiceFactory = new NugetPackageServiceFactory();

        return new NugetPackageBadgeHandler(logger, nugetPackageServiceFactory);
    }

    private static GithubPackagesBadgeHandler CreateGithubPackagesBadgeHandler()
    {
        var logger = LoggerFactory.CreateLogger<GithubPackagesBadgeHandler>();
        return new GithubPackagesBadgeHandler(logger);
    }

    private static TestResultsBadgeHandler CreateTestResultsBadgeHandler()
    {
        var logger = LoggerFactory.CreateLogger<TestResultsBadgeHandler>();
        return new TestResultsBadgeHandler(logger);
    }

    private static TestResultRedirectionHandler CreateTestResultRedirectionHandler()
    {
        var logger = LoggerFactory.CreateLogger<TestResultRedirectionHandler>();
        return new TestResultRedirectionHandler(logger);
    }

    private static TestResultIngestionHandler CreateTestResultIngestionHandler()
    {
        var logger = LoggerFactory.CreateLogger<TestResultIngestionHandler>();
        return new TestResultIngestionHandler(logger);
    }
}
