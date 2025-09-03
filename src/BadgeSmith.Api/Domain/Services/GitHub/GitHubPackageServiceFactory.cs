using BadgeSmith.Api.Domain.AWS;
using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Domain.Services.Package;
using BadgeSmith.Api.Infrastructure.Caching;
using BadgeSmith.Api.Infrastructure.Http;
using BadgeSmith.Api.Infrastructure.Observability;

namespace BadgeSmith.Api.Domain.Services.GitHub;

internal class GitHubPackageServiceFactory : IGitHubPackageServiceFactory
{
    private static readonly Lazy<GitHubOrgSecretsService> GithubOrgSecretsServiceLazy = new(CreateGithubOrgSecretsService);
    private static readonly Lazy<GitHubPackageService> GitHubPackageServiceLazy = new(CreateGitHubPackageService);

    public GitHubOrgSecretsService GitHubOrgSecretsService => GithubOrgSecretsServiceLazy.Value;
    public GitHubPackageService GitHubPackageService => GitHubPackageServiceLazy.Value;

    private static GitHubOrgSecretsService CreateGithubOrgSecretsService()
    {
        var amazonDynamoDbClient = AwsClientFactory.AmazonDynamoDbClient;
        var amazonSecretsManagerClient = AwsClientFactory.AmazonSecretsManagerClient;

        var secretsTableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_ORG_SECRETS_TABLE");

        if (string.IsNullOrWhiteSpace(secretsTableName))
        {
            throw new InvalidOperationException("AWS_RESOURCE_ORG_SECRETS_TABLE environment variable is not set");
        }

        var githubSecretsLogger = LoggerFactory.CreateLogger<GitHubOrgSecretsService>();

        var memoryAppCache = new MemoryAppCache();
        return new GitHubOrgSecretsService(amazonSecretsManagerClient, amazonDynamoDbClient, secretsTableName, memoryAppCache, githubSecretsLogger);
    }

    private static GitHubPackageService CreateGitHubPackageService()
    {
        var githubClient = HttpClientFactory.CreateGithubClient();
        var githubOrgSecretsService = GithubOrgSecretsServiceLazy.Value;
        var nuGetVersionService = new NuGetVersionService();
        var memoryAppCache = new MemoryAppCache();
        var gitHubPackageLogger = LoggerFactory.CreateLogger<GitHubPackageService>();

        return new GitHubPackageService(githubClient, githubOrgSecretsService, nuGetVersionService, memoryAppCache, gitHubPackageLogger);
    }
}
