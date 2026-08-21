namespace BadgeSmith.Api.Core.Http;

/// <summary>
/// Cached upstream response payload with its replay validators, shared by the package provider services.
/// </summary>
/// <param name="Payload">The upstream response body to replay on 304.</param>
/// <param name="ETag">The upstream ETag stored verbatim, including any weak marker.</param>
/// <param name="LastModified">The upstream last-modified validator.</param>
internal sealed record UpstreamCacheEntry(string Payload, string? ETag, DateTimeOffset? LastModified);
