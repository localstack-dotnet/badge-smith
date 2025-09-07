using Amazon.DynamoDBv2;
using Amazon.SecretsManager;
using BadgeSmith.Api.Core.Caching;
using BadgeSmith.Api.Core.Http;
using BadgeSmith.Api.Core.Observability;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Cors;
using BadgeSmith.Api.Core.Security;
using BadgeSmith.Api.Core.Security.Contracts;
using BadgeSmith.Api.Core.Versioning;
using BadgeSmith.Api.Features.GitHub;
using BadgeSmith.Api.Features.GitHub.Contracts;
using BadgeSmith.Api.Features.HealthCheck;
using BadgeSmith.Api.Features.NuGet;
using BadgeSmith.Api.Features.NuGet.Contracts;
using BadgeSmith.Api.Features.TestResults;
using BadgeSmith.Api.Features.TestResults.Contracts;
using BadgeSmith.Api.Features.TestResults.Handlers;
using Microsoft.Extensions.Caching.Memory;

namespace BadgeSmith.Api.Core;

internal static class ApplicationRegistry
{
    private static readonly Lazy<ApiRouter> ApiRouterLazy = new(BuildApiRouter);

    private static readonly Lazy<IHealthCheckHandler> HealthCheckHandlerLazy = new(CreateHealthCheckHandler);
    private static readonly Lazy<INugetPackageBadgeHandler> NugetPackageBadgeHandlerLazy = new(CreateNugetPackageBadgeHandler);
    private static readonly Lazy<IGithubPackagesBadgeHandler> GithubPackagesBadgeHandlerLazy = new(CreateGithubPackagesBadgeHandler);
    private static readonly Lazy<ITestResultsBadgeHandler> TestResultsBadgeHandlerLazy = new(CreateTestResultsBadgeHandler);
    private static readonly Lazy<ITestResultRedirectionHandler> TestResultRedirectionHandlerLazy = new(CreateTestResultRedirectionHandler);
    private static readonly Lazy<ITestResultIngestionHandler> TestResultIngestionHandlerLazy = new(CreateTestResultIngestionHandler);

    private static readonly Lazy<MemoryAppCache> MemoryAppCacheLazy = new(new MemoryAppCache(new MemoryCache(new MemoryCacheOptions())));
    private static readonly Lazy<AmazonDynamoDBClient> DynamoDbClientLazy = new(AwsClientBuilder.CreateAwsClient<AmazonDynamoDBClient>);
    private static readonly Lazy<AmazonSecretsManagerClient> AmazonSecretsManagerClientLazy = new(AwsClientBuilder.CreateAwsClient<AmazonSecretsManagerClient>);

    private static readonly Lazy<IGitHubOrgSecretsService> GithubOrgSecretsServiceLazy = new(BuildGithubOrgSecretsService);
    private static readonly Lazy<INonceService> NonceServiceLazy = new(BuildNonceService);
    private static readonly Lazy<IHmacAuthenticationService> HmacAuthenticationServiceLazy = new(BuildHmacAuthenticationService);

    private static readonly Lazy<INuGetPackageService> NuGetPackageServiceLazy = new(BuildNuGetPackageService);
    private static readonly Lazy<IGitHubPackageService> GitHubPackageServiceLazy = new(BuildGitHubPackageService);
    private static readonly Lazy<ITestResultsService> TestResultsServiceLazy = new(BuildTestResultsService);

    public static ApiRouter ApiRouter => ApiRouterLazy.Value;

    public static IHealthCheckHandler HealthCheckHandler => HealthCheckHandlerLazy.Value;

    public static INugetPackageBadgeHandler NugetPackageBadgeHandler => NugetPackageBadgeHandlerLazy.Value;

    public static IGithubPackagesBadgeHandler GithubPackagesBadgeHandler => GithubPackagesBadgeHandlerLazy.Value;

    public static ITestResultsBadgeHandler TestResultsBadgeHandler => TestResultsBadgeHandlerLazy.Value;

    public static ITestResultRedirectionHandler TestResultRedirectionHandler => TestResultRedirectionHandlerLazy.Value;

    public static ITestResultIngestionHandler TestResultIngestionHandler => TestResultIngestionHandlerLazy.Value;

    public static MemoryAppCache MemoryAppCache => MemoryAppCacheLazy.Value;
    public static IAmazonDynamoDB AmazonDynamoDbClient => DynamoDbClientLazy.Value;
    public static IAmazonSecretsManager AmazonSecretsManagerClient => AmazonSecretsManagerClientLazy.Value;

    public static IGitHubOrgSecretsService GitHubOrgSecretsService => GithubOrgSecretsServiceLazy.Value;
    public static INonceService NonceService => NonceServiceLazy.Value;
    public static IHmacAuthenticationService HmacAuthenticationService => HmacAuthenticationServiceLazy.Value;

    public static ITestResultsService TestResultsService => TestResultsServiceLazy.Value;
    public static INuGetPackageService NuGetPackageService => NuGetPackageServiceLazy.Value;
    public static IGitHubPackageService GitHubPackageService => GitHubPackageServiceLazy.Value;

    private static ApiRouter BuildApiRouter()
    {
        var logger = LoggerFactory.CreateLogger<ApiRouter>();
        var routeResolver = new RouteResolver(RouteTable.Routes);

        var corsHandler = new CorsHandler(routeResolver, LoggerFactory.CreateLogger<CorsHandler>(), new CorsOptions
        {
            AllowCredentials = false,
            UseWildcardWhenNoCredentials = true,
            MaxAgeSeconds = 3600,
        });

        return new ApiRouter(logger, routeResolver, corsHandler);
    }

    private static HealthCheckHandler CreateHealthCheckHandler()
    {
        var logger = LoggerFactory.CreateLogger<HealthCheckHandler>();

        return new HealthCheckHandler(logger);
    }

    private static NuGetPackageBadgeHandler CreateNugetPackageBadgeHandler()
    {
        var logger = LoggerFactory.CreateLogger<NuGetPackageBadgeHandler>();

        return new NuGetPackageBadgeHandler(logger, NuGetPackageService);
    }

    private static GithubPackagesBadgeHandler CreateGithubPackagesBadgeHandler()
    {
        var logger = LoggerFactory.CreateLogger<GithubPackagesBadgeHandler>();

        return new GithubPackagesBadgeHandler(logger, GitHubOrgSecretsService, GitHubPackageService);
    }

    private static TestResultsBadgeHandler CreateTestResultsBadgeHandler()
    {
        var logger = LoggerFactory.CreateLogger<TestResultsBadgeHandler>();

        return new TestResultsBadgeHandler(logger, TestResultsService);
    }

    private static TestResultRedirectionHandler CreateTestResultRedirectionHandler()
    {
        var logger = LoggerFactory.CreateLogger<TestResultRedirectionHandler>();

        return new TestResultRedirectionHandler(logger, TestResultsService);
    }

    private static TestResultIngestionHandler CreateTestResultIngestionHandler()
    {
        var logger = LoggerFactory.CreateLogger<TestResultIngestionHandler>();

        return new TestResultIngestionHandler(logger, HmacAuthenticationService, TestResultsService);
    }

    private static GitHubOrgSecretsService BuildGithubOrgSecretsService()
    {
        var secretsTableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_ORG_SECRETS_TABLE");

        if (string.IsNullOrWhiteSpace(secretsTableName))
        {
            throw new InvalidOperationException("AWS_RESOURCE_ORG_SECRETS_TABLE environment variable is not set");
        }

        var githubSecretsLogger = LoggerFactory.CreateLogger<GitHubOrgSecretsService>();

        return new GitHubOrgSecretsService(AmazonSecretsManagerClient, AmazonDynamoDbClient, secretsTableName, MemoryAppCache, githubSecretsLogger);
    }

    private static NonceService BuildNonceService()
    {
        var nonceTableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_NONCE_TABLE");

        if (string.IsNullOrWhiteSpace(nonceTableName))
        {
            throw new InvalidOperationException("AWS_RESOURCE_NONCE_TABLE environment variable is not set");
        }

        var logger = LoggerFactory.CreateLogger<NonceService>();

        return new NonceService(AmazonDynamoDbClient, MemoryAppCache, logger, nonceTableName);
    }

    private static HmacAuthenticationService BuildHmacAuthenticationService()
    {
        var repoSecretsService = GitHubOrgSecretsService;
        var nonceService = NonceService;

        var logger = LoggerFactory.CreateLogger<HmacAuthenticationService>();

        return new HmacAuthenticationService(repoSecretsService, nonceService, logger);
    }

    private static NuGetPackageService BuildNuGetPackageService()
    {
        var logger = LoggerFactory.CreateLogger<NuGetPackageService>();
        var httpClient = HttpClientFactory.CreateNuGetClient();
        var nuGetVersionService = new NuGetVersionService();

        return new NuGetPackageService(nuGetVersionService, logger, httpClient, MemoryAppCache);
    }

    private static GitHubPackageService BuildGitHubPackageService()
    {
        var githubClient = HttpClientFactory.CreateGithubClient();
        var nuGetVersionService = new NuGetVersionService();
        var gitHubPackageLogger = LoggerFactory.CreateLogger<GitHubPackageService>();

        return new GitHubPackageService(githubClient, nuGetVersionService, MemoryAppCache, gitHubPackageLogger);
    }

    private static TestResultsService BuildTestResultsService()
    {
        var testResultsTableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_TEST_RESULTS_TABLE");

        if (string.IsNullOrWhiteSpace(testResultsTableName))
        {
            throw new InvalidOperationException("AWS_RESOURCE_TEST_RESULTS_TABLE environment variable is not set");
        }

        var logger = LoggerFactory.CreateLogger<TestResultsService>();

        return new TestResultsService(AmazonDynamoDbClient, testResultsTableName, logger);
    }
}
