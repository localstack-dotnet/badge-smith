using System.Globalization;
using System.Text.RegularExpressions;
using BadgeSmith.Api.Routing.Contracts;

namespace BadgeSmith.Api.Routing.Patterns;

internal sealed class RegexPattern : IRoutePattern
{
    private readonly Regex _regex;
    private readonly string[] _namedGroups;

    public RegexPattern(Func<Regex> factory)
    {
        _regex = factory();
        _namedGroups = Array.FindAll(_regex.GetGroupNames(), name => !int.TryParse(name, CultureInfo.InvariantCulture, out var _));
    }

    public bool TryMatch(ReadOnlySpan<char> path, ref RouteValues values)
    {
        // .NET 8 Regex still needs string here; unavoidable alloc
        var m = _regex.Match(path.ToString());
        if (!m.Success)
        {
            return false;
        }

        // Use pre-computed named groups array
        foreach (var name in _namedGroups)
        {
            var g = m.Groups[name];
            if (g.Success)
            {
                values.Set(name, g.Index, g.Length);
            }
        }

        return true;
    }
}
