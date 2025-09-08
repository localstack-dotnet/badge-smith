using BadgeSmith.Api.Core.Routing;

namespace BadgeSmith.Api.Tests.TestHelpers;

internal static class RouteTestExtensions
{
    public static string? GetParameterValue(this RouteValues values, string parameterName)
    {
        return values.TryGetString(parameterName, out var value) ? value : null;
    }

    public static Dictionary<string, string> ToDictionary(this RouteValues values)
    {
        var immutableDict = values.ToImmutableDictionary();
        return new Dictionary<string, string>(immutableDict, StringComparer.OrdinalIgnoreCase);
    }
}
