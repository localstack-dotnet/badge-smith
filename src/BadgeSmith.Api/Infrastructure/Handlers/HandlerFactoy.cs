#pragma warning disable S125, RCS1093

using BadgeSmith.Api.Domain.Services.Authentication;
using BadgeSmith.Api.Domain.Services.GitHub;
using BadgeSmith.Api.Domain.Services.Nuget;
using BadgeSmith.Api.Domain.Services.TestResults;
using BadgeSmith.Api.Infrastructure.Handlers.Contracts;
using BadgeSmith.Api.Infrastructure.Observability;

namespace BadgeSmith.Api.Infrastructure.Handlers;

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

    private static NuGetPackageBadgeHandler CreateNugetPackageBadgeHandler()
    {
        var logger = LoggerFactory.CreateLogger<NuGetPackageBadgeHandler>();
        var nugetPackageServiceFactory = new NuGetPackageServiceFactory();

        return new NuGetPackageBadgeHandler(logger, nugetPackageServiceFactory.NuGetPackageService);
    }

    private static GithubPackagesBadgeHandler CreateGithubPackagesBadgeHandler()
    {
        var githubPackagesLogger = LoggerFactory.CreateLogger<GithubPackagesBadgeHandler>();
        var packageFactory = new GitHubPackageServiceFactory();
        var authServiceFactory = new AuthenticationServiceFactory();

        return new GithubPackagesBadgeHandler
        (
            githubPackagesLogger,
            authServiceFactory.GitHubOrgSecretsService,
            packageFactory.GitHubPackageService
        );
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
        var authFactory = new AuthenticationServiceFactory();
        var testResultsFactory = new TestResultsServiceFactory();
        return new TestResultIngestionHandler(
            logger,
            authFactory.HmacAuthenticationService,
            testResultsFactory.TestResultsService);
    }
}
