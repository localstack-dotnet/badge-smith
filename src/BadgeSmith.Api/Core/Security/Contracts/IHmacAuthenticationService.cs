namespace BadgeSmith.Api.Core.Security.Contracts;

/// <summary>
/// Service for validating HMAC-SHA256 signatures with replay protection.
/// Used to authenticate test result ingestion requests from CI/CD systems.
/// </summary>
internal interface IHmacAuthenticationService
{
    /// <summary>
    /// Validates an HMAC-signed request with nonce-based replay protection.
    /// Extracts organization from route parameters for secret lookup.
    /// </summary>
    /// <param name="authContext">The auth context containing request and route parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication result indicating success or specific failure type</returns>
    public Task<HmacAuthenticationResult> ValidateRequestAsync(HmacAuthContext authContext, CancellationToken ct = default);
}
