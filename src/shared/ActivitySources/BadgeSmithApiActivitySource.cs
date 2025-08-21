using System.Diagnostics;

namespace BadgeSmith;

internal static class BadgeSmithApiActivitySource
{
    public const string ActivitySourceName = "BadgeSmith.Api";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
