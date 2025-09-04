using BadgeSmith.Api.Domain.AWS;
using BadgeSmith.Api.Domain.Services.Authentication.Contracts;
using BadgeSmith.Api.Infrastructure.Caching;
using BadgeSmith.Api.Infrastructure.Observability;

namespace BadgeSmith.Api.Domain.Services.Authentication;

internal class AuthenticationServiceFactory : IAuthenticationServiceFactory
{
    private static readonly Lazy<IRepoSecretsService> RepoSecretsServiceLazy = new(CreateRepoSecretsService);
    private static readonly Lazy<INonceService> NonceServiceLazy = new(CreateNonceService);
    private static readonly Lazy<IHmacAuthenticationService> HmacAuthenticationServiceLazy = new(CreateHmacAuthenticationService);

    public IRepoSecretsService RepoSecretsService => RepoSecretsServiceLazy.Value;
    public INonceService NonceService => NonceServiceLazy.Value;
    public IHmacAuthenticationService HmacAuthenticationService => HmacAuthenticationServiceLazy.Value;

    private static RepoSecretsService CreateRepoSecretsService()
    {
        var amazonDynamoDbClient = AwsClientFactory.AmazonDynamoDbClient;
        var amazonSecretsManagerClient = AwsClientFactory.AmazonSecretsManagerClient;

        var secretsTableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_ORG_SECRETS_TABLE");

        if (string.IsNullOrWhiteSpace(secretsTableName))
        {
            throw new InvalidOperationException("AWS_RESOURCE_ORG_SECRETS_TABLE environment variable is not set");
        }

        var logger = LoggerFactory.CreateLogger<RepoSecretsService>();
        var cache = new MemoryAppCache();

        return new RepoSecretsService(amazonSecretsManagerClient, amazonDynamoDbClient, secretsTableName, cache, logger);
    }

    private static NonceService CreateNonceService()
    {
        var amazonDynamoDbClient = AwsClientFactory.AmazonDynamoDbClient;

        var nonceTableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_NONCE_TABLE");

        if (string.IsNullOrWhiteSpace(nonceTableName))
        {
            throw new InvalidOperationException("AWS_RESOURCE_NONCE_TABLE environment variable is not set");
        }

        var logger = LoggerFactory.CreateLogger<NonceService>();
        var cache = new MemoryAppCache();

        return new NonceService(amazonDynamoDbClient, cache, logger, nonceTableName);
    }

    private static HmacAuthenticationService CreateHmacAuthenticationService()
    {
        var repoSecretsService = CreateRepoSecretsService();
        var nonceService = CreateNonceService();
        var logger = LoggerFactory.CreateLogger<HmacAuthenticationService>();

        return new HmacAuthenticationService(repoSecretsService, nonceService, logger);
    }
}
