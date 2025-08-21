using System.Diagnostics;

namespace BadgeSmith;

public static class BadgeSmithInfrastructureActivitySource
{
    public const string ActivitySourceName = "BadgeSmith.Infrastructure";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
