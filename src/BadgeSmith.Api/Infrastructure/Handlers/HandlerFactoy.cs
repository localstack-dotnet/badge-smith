#pragma warning disable S125, RCS1093

using Amazon.DynamoDBv2;
using Amazon.SecretsManager;
using BadgeSmith.Api.Domain.Services.Github;
using BadgeSmith.Api.Domain.Services.Nuget;
using BadgeSmith.Api.Infrastructure.Caching;
using BadgeSmith.Api.Infrastructure.Handlers.Contracts;
using BadgeSmith.Api.Infrastructure.Observability;
using LocalStack.Client;
using LocalStack.Client.Options;

namespace BadgeSmith.Api.Infrastructure.Handlers;

internal class HandlerFactory : IHandlerFactory
{
    private static readonly Lazy<IHealthCheckHandler> HealthCheckHandlerLazy = new(CreateHealthCheckHandler);
    private static readonly Lazy<INugetPackageBadgeHandler> NugetPackageBadgeHandlerLazy = new(CreateNugetPackageBadgeHandler);
    private static readonly Lazy<IGithubPackagesBadgeHandler> GithubPackagesBadgeHandlerLazy = new(CreateGithubPackagesBadgeHandler);
    private static readonly Lazy<ITestResultsBadgeHandler> TestResultsBadgeHandlerLazy = new(CreateTestResultsBadgeHandler);
    private static readonly Lazy<ITestResultRedirectionHandler> TestResultRedirectionHandlerLazy = new(CreateTestResultRedirectionHandler);
    private static readonly Lazy<ITestResultIngestionHandler> TestResultIngestionHandlerLazy = new(CreateTestResultIngestionHandler);

    private static readonly Lazy<IAmazonSecretsManager> SecretsManagerLazy = new(CreateSecretsManagerClient());
    private static readonly Lazy<IAmazonDynamoDB> DynamoDbClientLazy = new(CreateDynamoDbClient);

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

        return new NuGetPackageBadgeHandler(logger, nugetPackageServiceFactory);
    }

    private static GithubPackagesBadgeHandler CreateGithubPackagesBadgeHandler()
    {
        var githubPackagesLogger = LoggerFactory.CreateLogger<GithubPackagesBadgeHandler>();
        var githubSecretsLogger = LoggerFactory.CreateLogger<GithubOrgSecretsService>();

        var secretsTableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_ORG_SECRETS_TABLE");

        if (string.IsNullOrWhiteSpace(secretsTableName))
        {
            throw new InvalidOperationException("AWS_RESOURCE_ORG_SECRETS_TABLE environment variable is not set");
        }

        var memoryAppCache = new MemoryAppCache();
        var githubOrgSecretsService = new GithubOrgSecretsService(SecretsManagerLazy.Value, DynamoDbClientLazy.Value, secretsTableName, memoryAppCache, githubSecretsLogger);
        return new GithubPackagesBadgeHandler(githubPackagesLogger, githubOrgSecretsService);
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

    private static AmazonDynamoDBClient CreateDynamoDbClient()
    {
        if (!Settings.UseLocalStack)
        {
            return new AmazonDynamoDBClient();
        }

        var uri = new Uri(Settings.LocalStackEndpoint!);
        var localStackHost = uri.Host;
        var localStackPort = uri.Port;
        return SessionStandalone
            .Init()
            .WithConfigurationOptions(new ConfigOptions(localStackHost, edgePort: localStackPort))
            .WithSessionOptions(new SessionOptions(regionName: Environment.GetEnvironmentVariable("AWS_REGION")!))
            .Create()
            .CreateClientByImplementation<AmazonDynamoDBClient>();
    }

    private static AmazonSecretsManagerClient CreateSecretsManagerClient()
    {
        if (!Settings.UseLocalStack)
        {
            return new AmazonSecretsManagerClient();
        }

        var uri = new Uri(Settings.LocalStackEndpoint!);
        var localStackHost = uri.Host;
        var localStackPort = uri.Port;
        return SessionStandalone
            .Init()
            .WithConfigurationOptions(new ConfigOptions(localStackHost, edgePort: localStackPort))
            .WithSessionOptions(new SessionOptions(regionName: Environment.GetEnvironmentVariable("AWS_REGION")!))
            .Create()
            .CreateClientByImplementation<AmazonSecretsManagerClient>();
    }
}
