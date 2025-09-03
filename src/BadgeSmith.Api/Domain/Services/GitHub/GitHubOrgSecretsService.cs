using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using ResourceNotFoundException = Amazon.SecretsManager.Model.ResourceNotFoundException;

namespace BadgeSmith.Api.Domain.Services.GitHub;

internal sealed class GitHubOrgSecretsService : IGitHubOrgSecretsService
{
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly IAppCache _cache;
    private readonly ILogger<GitHubOrgSecretsService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public GitHubOrgSecretsService(
        IAmazonSecretsManager secretsManager,
        IAmazonDynamoDB dynamoDb,
        string tableName,
        IAppCache cache,
        ILogger<GitHubOrgSecretsService> logger)
    {
        _secretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _tableName = tableName;
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GithubSecretResult> GetGitHubTokenAsync(string organizationName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationName);

        var orgLower = organizationName.ToLowerInvariant();
        var cacheKey = $"github_token:{orgLower}";

        if (_cache.TryGetValue<string>(cacheKey, out var cachedToken))
        {
            return cachedToken;
        }

        var getItemRequest = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new($"ORG#{orgLower}"),
                ["SK"] = new("CONST#GITHUB"),
            },
            ProjectionExpression = "SecretName",
            ConsistentRead = false,
        };

        var getItemResponse = await _dynamoDb.GetItemAsync(getItemRequest, ct).ConfigureAwait(false);

        if (!IsSuccessStatusCode(getItemResponse.HttpStatusCode))
        {
            return new Error($"Failed to retrieve GitHub token for organization '{orgLower}'");
        }

        if (getItemResponse.Item == null || getItemResponse.Item.Count == 0)
        {
            _logger.LogWarning("No secret mapping found for organization {Organization}", orgLower);
            return new SecretNotFound($"No secret mapping found for organization {orgLower}");
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
                return new Error($"Failed to retrieve GitHub token for organization '{orgLower}'");
            }

            var token = secretValueResponse.SecretString;
            _cache.Set(cacheKey, token, CacheTtl);

            return token;
        }
        catch (ResourceNotFoundException ex)
        {
            _logger.LogWarning(ex, "Secret {SecretName} not found for organization {Organization}", secretName, orgLower);
            return new SecretNotFound($"Secret '{secretName}' not found for organization '{orgLower}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve GitHub token for organization {Organization} from secret {SecretName}", orgLower, secretName);
            return new Error($"Failed to retrieve GitHub token for organization '{orgLower}' from secret {secretName}");
        }
    }

    public static bool IsSuccessStatusCode(HttpStatusCode statusCode) => (int)statusCode is >= 200 and <= 299;
}
