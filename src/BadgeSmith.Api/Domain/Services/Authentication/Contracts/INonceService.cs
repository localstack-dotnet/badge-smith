namespace BadgeSmith.Api.Domain.Services.Authentication.Contracts;

/// <summary>
/// Service for managing nonce-based replay attack prevention using DynamoDB.
/// Stores used nonces with TTL to prevent the same request from being processed twice.
/// </summary>
internal interface INonceService
{
    /// <summary>
    /// Validates that a nonce hasn't been used before and marks it as used.
    /// This is an atomic operation to prevent race conditions.
    /// </summary>
    /// <param name="nonce">Unique nonce value from the request</param>
    /// <param name="repoIdentifier">Repository identifier for partitioning</param>
    /// <param name="requestTimestamp">Request timestamp for TTL calculation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating if the nonce is valid (unused) or has been seen before</returns>
    public Task<NonceValidationResult> ValidateAndMarkNonceAsync(string nonce, string repoIdentifier, DateTimeOffset requestTimestamp, CancellationToken ct = default);
}
