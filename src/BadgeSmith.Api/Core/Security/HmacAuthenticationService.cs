#pragma warning disable CA1873 // Replace with LoggerMessage source-generated logging.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BadgeSmith.Api.Core.Security.Contracts;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Core.Security;

/// <summary>
/// Service for validating HMAC-SHA256 signatures with replay protection.
/// Used to authenticate test result ingestion requests from CI/CD systems.
/// </summary>
internal sealed class HmacAuthenticationService : IHmacAuthenticationService
{
    private readonly IGitHubOrgSecretsService _gitHubOrgSecretsService;
    private readonly INonceService _nonceService;
    private readonly ILogger<HmacAuthenticationService> _logger;

    private static readonly TimeSpan MaxTimestampAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxTimestampSkew = TimeSpan.FromMinutes(1);
    private const string TokenType = "TestData";

    public HmacAuthenticationService(IGitHubOrgSecretsService gitHubOrgSecretsService, INonceService nonceService, ILogger<HmacAuthenticationService> logger)
    {
        _gitHubOrgSecretsService = gitHubOrgSecretsService ?? throw new ArgumentNullException(nameof(gitHubOrgSecretsService));
        _nonceService = nonceService ?? throw new ArgumentNullException(nameof(nonceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HmacAuthenticationResult> ValidateRequestAsync(HmacAuthContext authContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(HmacAuthenticationService)}.{nameof(ValidateRequestAsync)}");

        ValidateHmacAuthContext(authContext);

        var timestamp = authContext.Timestamp.Trim();
        var nonce = authContext.Nonce.Trim();

        if (!TryParseTimestamp(timestamp, out var requestTimestamp, out var timestampError))
        {
            return timestampError;
        }

        var repoIdentifier = $"{authContext.Owner.ToLowerInvariant()}/{authContext.Repo.ToLowerInvariant()}/{authContext.Platform.ToLowerInvariant()}/{authContext.Branch}";

        var secretResult = await _gitHubOrgSecretsService.GetGitHubTokenAsync(authContext.Owner, TokenType, ct).ConfigureAwait(false);
        if (secretResult is { IsSuccess: false, GithubSecret: null })
        {
            return secretResult.Failure.Match<HmacAuthenticationResult>
            (
                notFound => new RepoSecretNotFound(notFound.Reason),
                error => error
            );
        }

        var secret = secretResult.GithubSecret!;

        if (!ValidateHmacSignature(authContext, timestamp, nonce, secret))
        {
            _logger.LogWarning("Invalid HMAC signature for repository {RepoIdentifier}", repoIdentifier);
            return new InvalidSignature("HMAC signature verification failed");
        }

        var nonceResult = await _nonceService.ValidateAndMarkNonceAsync(nonce, repoIdentifier, requestTimestamp, ct).ConfigureAwait(false);

        if (!nonceResult.IsSuccess)
        {
            return nonceResult.Failure.Match<HmacAuthenticationResult>
            (
                alreadyUsed => alreadyUsed,
                error => error
            );
        }

        _logger.LogInformation("Successfully authenticated request for repository {RepoIdentifier}", repoIdentifier);
        return new AuthenticatedRequest(repoIdentifier, requestTimestamp);
    }

    [SuppressMessage("Usage", "MA0015:Specify the parameter name in ArgumentException")]
    private static void ValidateHmacAuthContext(HmacAuthContext routeContext)
    {
        ArgumentNullException.ThrowIfNull(routeContext);
        ArgumentNullException.ThrowIfNull(routeContext.Owner);
        ArgumentNullException.ThrowIfNull(routeContext.Repo);
        ArgumentNullException.ThrowIfNull(routeContext.Platform);
        ArgumentNullException.ThrowIfNull(routeContext.Branch);
        ArgumentNullException.ThrowIfNull(routeContext.Signature);
        ArgumentNullException.ThrowIfNull(routeContext.Timestamp);
        ArgumentNullException.ThrowIfNull(routeContext.Nonce);
        ArgumentNullException.ThrowIfNull(routeContext.RequestBody);
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

    private static bool ValidateHmacSignature(HmacAuthContext authContext, string timestamp, string nonce, string secret)
    {
        var providedSignature = authContext.Signature;
        if (!providedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHash = providedSignature.AsSpan(7);
        if (providedHash.Length != 64)
        {
            return false;
        }

        Span<byte> providedHashBytes = stackalloc byte[32];
        var status = Convert.FromHexString(providedHash, providedHashBytes, out var charsConsumed, out var bytesWritten);
        if (status != OperationStatus.Done || charsConsumed != providedHash.Length || bytesWritten != providedHashBytes.Length)
        {
            return false;
        }

        var canonicalText = HmacCanonicalRequest.CreateCanonicalText(
            authContext.Platform,
            authContext.Owner,
            authContext.Repo,
            authContext.Branch,
            timestamp,
            nonce,
            authContext.RequestBody);
        var computedHashBytes = ComputeHmacSha256(canonicalText, secret);
        return CryptographicOperations.FixedTimeEquals(providedHashBytes, computedHashBytes);
    }

    private static byte[] ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        return hmac.ComputeHash(payloadBytes);
    }
}

#pragma warning restore CA1873
