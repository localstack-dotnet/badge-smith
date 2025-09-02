using BadgeSmith.Api.Domain.AWS;
using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Infrastructure.Caching;
using BadgeSmith.Api.Infrastructure.Observability;

namespace BadgeSmith.Api.Domain.Services.Github;

internal class GithubPackageServiceFactory : IGithubPackageServiceFactory
{
    private static readonly Lazy<GithubOrgSecretsService> GithubOrgSecretsServiceLazy = new(CreateGithubOrgSecretsService);

    public GithubOrgSecretsService GithubOrgSecretsService => GithubOrgSecretsServiceLazy.Value;

    private static GithubOrgSecretsService CreateGithubOrgSecretsService()
    {
        var amazonDynamoDbClient = AwsClientFactory.AmazonDynamoDbClient;
        var amazonSecretsManagerClient = AwsClientFactory.AmazonSecretsManagerClient;

        var secretsTableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_ORG_SECRETS_TABLE");

        if (string.IsNullOrWhiteSpace(secretsTableName))
        {
            throw new InvalidOperationException("AWS_RESOURCE_ORG_SECRETS_TABLE environment variable is not set");
        }

        var githubSecretsLogger = LoggerFactory.CreateLogger<GithubOrgSecretsService>();

        var memoryAppCache = new MemoryAppCache();
        return new GithubOrgSecretsService(amazonSecretsManagerClient, amazonDynamoDbClient, secretsTableName, memoryAppCache, githubSecretsLogger);
    }
}
