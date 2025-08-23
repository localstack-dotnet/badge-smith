using System.Globalization;
using System.Text.RegularExpressions;
using BadgeSmith.Api.Routing.Contracts;

namespace BadgeSmith.Api.Routing.Patterns;

internal sealed class RegexPattern : IRoutePattern
{
    private readonly Func<Regex> _factory;

    public RegexPattern(Func<Regex> factory) => _factory = factory;

    public bool TryMatch(ReadOnlySpan<char> path, ref RouteValues values)
    {
        var m = _factory().Match(path.ToString()); // first call creates regex; subsequent are cached
        if (!m.Success)
        {
            return false;
        }

        // capture only *named* groups
        foreach (var name in _factory().GetGroupNames())
        {
            if (int.TryParse(name, CultureInfo.InvariantCulture, out var _))
            {
                continue;
            }

            var g = m.Groups[name];
            if (g.Success)
            {
                values.Set(name.AsSpan(), g.Index, g.Length);
            }
        }

        return true;
    }
}
