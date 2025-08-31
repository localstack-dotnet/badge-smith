using System.Globalization;

namespace BadgeSmith.Api.Infrastructure.Observability.Loggers;

internal static class SimpleLogger
{
    private static string Timestamp => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    public static void LogInformation(string category, string message)
    {
        Console.WriteLine($"{Timestamp}\tinfo: {category}[0]\t{message}");
    }

    public static void LogWarning(string category, string message)
    {
        Console.WriteLine($"{Timestamp}\twarn: {category}[0]\t{message}");
    }

    public static void LogError(string category, string message)
    {
        Console.WriteLine($"{Timestamp}\tfail: {category}[0]\t{message}");
    }

    public static void LogError(string category, Exception ex, string message)
    {
        Console.WriteLine($"{Timestamp}\tfail: {category}[0]\t{message}{Environment.NewLine}{ex}");
    }

    public static void LogDebug(string category, string message)
    {
        Console.WriteLine($"{Timestamp}\tdbug: {category}[0]\t{message}");
    }
}
