using BadgeSmith.Api.Core.Routing.Helpers;

namespace BadgeSmith.Api.Features;

/// <summary>
/// Named product cache presets. Per-call policy construction in handlers is prohibited; a new policy
/// is a new named preset.
/// </summary>
internal static class BadgeResponsePolicy
{
    public static PublicCachePolicy PublicCache { get; } = new(
        sharedMaxAge: TimeSpan.FromSeconds(600),
        clientMaxAge: TimeSpan.FromSeconds(300),
        staleWhileRevalidate: TimeSpan.FromSeconds(1200),
        staleIfError: TimeSpan.FromSeconds(3600));
}
