namespace BadgeSmith.Api.Core.Routing.Helpers;

/// <summary>
/// Immutable public cache policy that precomputes its deterministic Cache-Control header value once.
/// Deliberately a validated sealed class, not a record: <c>with</c> or init setters would bypass
/// constructor validation, and the preset is consumed by reference without value equality.
/// </summary>
internal sealed class PublicCachePolicy
{
    private const int MaxDeltaSeconds = int.MaxValue;

    public PublicCachePolicy(
        TimeSpan sharedMaxAge,
        TimeSpan clientMaxAge,
        TimeSpan staleWhileRevalidate,
        TimeSpan staleIfError)
    {
        SharedMaxAge = ValidateDeltaSeconds(sharedMaxAge);
        ClientMaxAge = ValidateDeltaSeconds(clientMaxAge);
        StaleWhileRevalidate = ValidateDeltaSeconds(staleWhileRevalidate);
        StaleIfError = ValidateDeltaSeconds(staleIfError);

        CacheControl =
            $"public, s-maxage={(int)SharedMaxAge.TotalSeconds}, max-age={(int)ClientMaxAge.TotalSeconds}"
            + $", stale-while-revalidate={(int)StaleWhileRevalidate.TotalSeconds}, stale-if-error={(int)StaleIfError.TotalSeconds}";
    }

    /// <summary>Gets the precomputed deterministic Cache-Control value.</summary>
    public string CacheControl { get; }

    public TimeSpan SharedMaxAge { get; }

    public TimeSpan ClientMaxAge { get; }

    public TimeSpan StaleWhileRevalidate { get; }

    public TimeSpan StaleIfError { get; }

    private static TimeSpan ValidateDeltaSeconds(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Cache delta-seconds cannot be negative.");
        }

        if (value.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Cache delta-seconds must be whole seconds.");
        }

        if (value.Ticks / TimeSpan.TicksPerSecond > MaxDeltaSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Cache delta-seconds exceed the supported range.");
        }

        return value;
    }
}
