using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using BadgeSmith.Api.Domain.Services.Authentication.Contracts;
using BadgeSmith.Api.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using ResourceNotFoundException = Amazon.SecretsManager.Model.ResourceNotFoundException;

namespace BadgeSmith.Api.Domain.Services.Authentication;

/// <summary>
/// Service for retrieving repository-specific HMAC secrets from AWS Secrets Manager.
/// Follows the same pattern as GitHubOrgSecretsService but for repo-level authentication.
/// </summary>
internal sealed class RepoSecretsService : IRepoSecretsService
{
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly IAppCache _cache;
    private readonly ILogger<RepoSecretsService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public RepoSecretsService(
        IAmazonSecretsManager secretsManager,
        IAmazonDynamoDB dynamoDb,
        string tableName,
        IAppCache cache,
        ILogger<RepoSecretsService> logger)
    {
        _secretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _tableName = tableName;
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RepoSecretResult> GetRepoSecretAsync(string repoIdentifier, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoIdentifier);

        var repoLower = repoIdentifier.ToLowerInvariant();
        var cacheKey = $"repo_secret:{repoLower}";

        if (_cache.TryGetValue<string>(cacheKey, out var cachedSecret))
        {
            return cachedSecret;
        }

        var getItemRequest = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new($"REPO#{repoLower}"),
                ["SK"] = new("CONST#HMAC"),
            },
            ProjectionExpression = "SecretName",
            ConsistentRead = false,
        };

        var getItemResponse = await _dynamoDb.GetItemAsync(getItemRequest, ct).ConfigureAwait(false);

        if (!IsSuccessStatusCode(getItemResponse.HttpStatusCode))
        {
            return new Error($"Failed to retrieve HMAC secret for repository '{repoLower}'");
        }

        if (getItemResponse.Item == null || getItemResponse.Item.Count == 0)
        {
            _logger.LogWarning("No secret mapping found for repository {Repository}", repoLower);
            return new RepoSecretNotFound($"No secret mapping found for repository {repoLower}");
        }

        var secretName = getItemResponse.Item["SecretName"].S;

        try
        {
            var secretValueRequest = new GetSecretValueRequest
            {
                SecretId = secretName,
            };

            var secretValueResponse = await _secretsManager.GetSecretValueAsync(secretValueRequest, ct).ConfigureAwait(false);

            if (!IsSuccessStatusCode(secretValueResponse.HttpStatusCode))
            {
                return new Error($"Failed to retrieve HMAC secret for repository '{repoLower}'");
            }

            var secret = secretValueResponse.SecretString;
            _cache.Set(cacheKey, secret, CacheTtl);

            return secret;
        }
        catch (ResourceNotFoundException ex)
        {
            _logger.LogWarning(ex, "Secret {SecretName} not found for repository {Repository}", secretName, repoLower);
            return new RepoSecretNotFound($"Secret '{secretName}' not found for repository '{repoLower}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve HMAC secret for repository {Repository} from secret {SecretName}", repoLower, secretName);
            return new Error($"Failed to retrieve HMAC secret for repository '{repoLower}' from secret {secretName}");
        }
    }

    private static bool IsSuccessStatusCode(HttpStatusCode statusCode) => (int)statusCode is >= 200 and <= 299;
}
