using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Domain.Services.Authentication.Contracts;

/// <summary>
/// Service for validating HMAC-SHA256 signatures with replay protection.
/// Used to authenticate test result ingestion requests from CI/CD systems.
/// </summary>
internal interface IHmacAuthenticationService
{
    /// <summary>
    /// Validates an HMAC-signed request with nonce-based replay protection.
    /// </summary>
    /// <param name="request">The API Gateway request containing headers and body</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication result indicating success or specific failure type</returns>
    public Task<HmacAuthenticationResult> ValidateRequestAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        CancellationToken ct = default);
}
