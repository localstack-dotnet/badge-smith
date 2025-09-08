namespace BadgeSmith.Api.Core.Security.Contracts;

/// <summary>
/// Service for managing nonce-based replay attack prevention using DynamoDB.
/// Stores used nonces with TTL to prevent the same request from being processed twice.
/// </summary>
internal interface INonceService
{
    public Task<NonceValidationResult> ValidateAndMarkNonceAsync(string nonce, string repoIdentifier, DateTimeOffset requestTimestamp, CancellationToken ct = default);
}
