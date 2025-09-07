using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BadgeSmith.Api.Core.Caching;
using BadgeSmith.Api.Core.Security.Contracts;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Core.Security;

/// <summary>
/// DynamoDB-based nonce service for preventing replay attacks.
/// Uses atomic conditional writes to ensure each nonce can only be used once.
/// </summary>
internal sealed class NonceService : INonceService
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IAppCache _cache;
    private readonly ILogger<NonceService> _logger;
    private readonly string _nonceTableName;

    private static readonly TimeSpan NonceTtl = TimeSpan.FromMinutes(45);

    public NonceService(IAmazonDynamoDB dynamoDb, IAppCache cache, ILogger<NonceService> logger, string nonceTableName)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _nonceTableName = nonceTableName ?? throw new ArgumentNullException(nameof(nonceTableName));
    }

    public async Task<NonceValidationResult> ValidateAndMarkNonceAsync(
        string nonce,
        string repoIdentifier,
        DateTimeOffset requestTimestamp,
        CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NonceService)}.{nameof(ValidateAndMarkNonceAsync)}");

        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoIdentifier);

        var cacheKey = $"nonce:{repoIdentifier}:{nonce}";
        if (_cache.TryGetValue<bool>(cacheKey, out _))
        {
            _logger.LogWarning("Nonce {Nonce} for repository {RepoIdentifier} already used (cached)", nonce, repoIdentifier);
            return new NonceAlreadyUsed($"Nonce '{nonce}' has already been used");
        }

        var partitionKey = $"NONCE#{repoIdentifier}";
        var ttlTimestamp = DateTimeOffset.UtcNow.Add(NonceTtl).ToUnixTimeSeconds();

        var putRequest = new PutItemRequest
        {
            TableName = _nonceTableName,
            Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new(partitionKey),
                ["SK"] = new(nonce),
                ["RequestTimestamp"] = new(requestTimestamp.ToString("O")),
                ["MarkedAt"] = new(DateTimeOffset.UtcNow.ToString("O")),
                ["TTL"] = new()
                {
                    N = ttlTimestamp.ToString(CultureInfo.InvariantCulture),
                },
            },
            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)",
        };

        try
        {
            await _dynamoDb.PutItemAsync(putRequest, ct).ConfigureAwait(false);

            _cache.Set(cacheKey, value: true, NonceTtl);

            _logger.LogDebug("Successfully marked nonce {Nonce} as used for repository {RepoIdentifier}", nonce, repoIdentifier);
            return new ValidNonce(nonce, DateTimeOffset.UtcNow);
        }
        catch (ConditionalCheckFailedException ex)
        {
            _cache.Set(cacheKey, value: true, NonceTtl);

            _logger.LogWarning(ex, "Nonce {Nonce} for repository {RepoIdentifier} already used (DynamoDB)", nonce, repoIdentifier);
            return new NonceAlreadyUsed($"Nonce '{nonce}' has already been used");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate nonce {Nonce} for repository {RepoIdentifier}", nonce, repoIdentifier);
            return new Error($"Failed to validate nonce: {ex.Message}");
        }
    }
}
