using Spectre.Console;
using System.Globalization;

namespace BadgeSmith.Tools.Services;

internal sealed class SpectreConsoleLogger : IToolLogger
{
    private readonly IAnsiConsole _console;
    private readonly TimeProvider _timeProvider;

    public SpectreConsoleLogger(IAnsiConsole console, TimeProvider timeProvider)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public void Trace(string message) => Log("TRACE", "grey", message);

    public void Debug(string message) => Log("DEBUG", "blue", message);

    public void Info(string message) => Log("INFO", "green", message);

    public void Warning(string message) => Log("WARN", "yellow", message);

    public void Error(string message) => Log("ERROR", "red", message);

    private void Log(string level, string color, string message)
    {
        var timestamp = _timeProvider.GetUtcNow().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        _console.MarkupLine($"[grey]{timestamp}Z[/] [{color}]{level}[/]: {Markup.Escape(message)}");
    }
}
