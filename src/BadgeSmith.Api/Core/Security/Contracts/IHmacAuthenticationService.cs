namespace BadgeSmith.Api.Core.Security.Contracts;

/// <summary>
/// Service for validating HMAC-SHA256 signatures with replay protection.
/// Used to authenticate test result ingestion requests from CI/CD systems.
/// </summary>
internal interface IHmacAuthenticationService
{
    public Task<HmacAuthenticationResult> ValidateRequestAsync(HmacAuthContext authContext, CancellationToken ct = default);
}
