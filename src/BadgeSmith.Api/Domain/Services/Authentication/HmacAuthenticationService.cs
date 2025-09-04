using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Domain.Services.Authentication.Contracts;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Domain.Services.Authentication;

/// <summary>
/// HMAC-SHA256 authentication service with replay protection.
/// Validates request signatures using repository-specific secrets and prevent replay attacks via nonces.
/// </summary>
internal sealed class HmacAuthenticationService : IHmacAuthenticationService
{
    private readonly IRepoSecretsService _repoSecretsService;
    private readonly INonceService _nonceService;
    private readonly ILogger<HmacAuthenticationService> _logger;

    private static readonly TimeSpan MaxTimestampAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxTimestampSkew = TimeSpan.FromMinutes(1);

    public HmacAuthenticationService(IRepoSecretsService repoSecretsService, INonceService nonceService, ILogger<HmacAuthenticationService> logger)
    {
        _repoSecretsService = repoSecretsService ?? throw new ArgumentNullException(nameof(repoSecretsService));
        _nonceService = nonceService ?? throw new ArgumentNullException(nameof(nonceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HmacAuthenticationResult> ValidateRequestAsync(APIGatewayHttpApiV2ProxyRequest request, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(HmacAuthenticationService)}.{nameof(ValidateRequestAsync)}");

        if (!TryExtractAuthHeaders(request.Headers, out var authHeaders, out var headerError))
        {
            return headerError;
        }

        var (signature, repoIdentifier, timestampStr, nonce) = authHeaders;

        if (!TryParseTimestamp(timestampStr, out var requestTimestamp, out var timestampError))
        {
            return timestampError;
        }

        var nonceResult = await _nonceService.ValidateAndMarkNonceAsync(nonce, repoIdentifier, requestTimestamp, ct).ConfigureAwait(false);
        if (!nonceResult.IsSuccess)
        {
            return nonceResult.Failure.Match<HmacAuthenticationResult>(alreadyUsed => alreadyUsed, error => error);
        }

        var secretResult = await _repoSecretsService.GetRepoSecretAsync(repoIdentifier, ct).ConfigureAwait(false);
        if (secretResult is { IsSuccess: false, Secret: null })
        {
            return secretResult.Failure.Match<HmacAuthenticationResult>
            (
                notFound => notFound,
                error => error
            );
        }

        var secret = secretResult.Secret!;

        if (!ValidateHmacSignature(signature, request.Body ?? string.Empty, secret))
        {
            _logger.LogWarning("Invalid HMAC signature for repository {RepoIdentifier}", repoIdentifier);
            return new InvalidSignature("HMAC signature verification failed");
        }

        _logger.LogInformation("Successfully authenticated request for repository {RepoIdentifier}", repoIdentifier);
        return new AuthenticatedRequest(repoIdentifier, requestTimestamp);
    }

    private static bool TryExtractAuthHeaders(
        IDictionary<string, string>? headers,
        out (string Signature, string RepoIdentifier, string Timestamp, string Nonce) authHeaders,
        out MissingAuthHeaders? error)
    {
        authHeaders = default;
        error = null;

        if (headers == null)
        {
            error = new MissingAuthHeaders("Request headers are missing");
            return false;
        }

        if (!headers.TryGetValue("x-signature", out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            error = new MissingAuthHeaders("X-Signature header is required");
            return false;
        }

        if (!headers.TryGetValue("x-repo-secret", out var repoIdentifier) || string.IsNullOrWhiteSpace(repoIdentifier))
        {
            error = new MissingAuthHeaders("X-Repo-Secret header is required");
            return false;
        }

        if (!headers.TryGetValue("x-timestamp", out var timestampStr) || string.IsNullOrWhiteSpace(timestampStr))
        {
            error = new MissingAuthHeaders("X-Timestamp header is required");
            return false;
        }

        if (!headers.TryGetValue("x-nonce", out var nonce) || string.IsNullOrWhiteSpace(nonce))
        {
            error = new MissingAuthHeaders("X-Nonce header is required");
            return false;
        }

        authHeaders = (signature.Trim(), repoIdentifier.Trim(), timestampStr.Trim(), nonce.Trim());
        return true;
    }

    private static bool TryParseTimestamp(string timestampStr, out DateTimeOffset requestTimestamp, out InvalidTimestamp? error)
    {
        error = null;

        if (!DateTimeOffset.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out requestTimestamp))
        {
            error = new InvalidTimestamp($"Invalid timestamp format: {timestampStr}. Expected ISO 8601 format.");
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var age = now - requestTimestamp;
        var skew = requestTimestamp - now;

        if (age > MaxTimestampAge)
        {
            error = new InvalidTimestamp($"Request timestamp is too old. Age: {age.TotalMinutes:F1} minutes, max allowed: {MaxTimestampAge.TotalMinutes} minutes.");
            return false;
        }

        if (skew > MaxTimestampSkew)
        {
            error = new InvalidTimestamp(
                $"Request timestamp is too far in the future. Skew: {skew.TotalMinutes:F1} minutes, max allowed: {MaxTimestampSkew.TotalMinutes} minutes.");
            return false;
        }

        return true;
    }

    private static bool ValidateHmacSignature(string providedSignature, string payload, string secret)
    {
        // Expected format: "sha256=<hex>"
        if (!providedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHash = providedSignature[7..];

        var computedHash = ComputeHmacSha256(payload, secret);

        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(providedHash), Convert.FromHexString(computedHash));
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
