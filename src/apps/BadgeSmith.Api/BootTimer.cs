using System.Diagnostics;
using Amazon.Lambda.Core;

namespace BadgeSmith.Api;

internal static class BootTimer
{
    private static readonly long T0 = Stopwatch.GetTimestamp();

    private static double MsSince(long ticks) =>
        (Stopwatch.GetTimestamp() - ticks) * 1000.0 / Stopwatch.Frequency;

    public static void Mark(ILambdaContext? ctx, string name)
    {
        var ms = MsSince(T0);
        if (ctx != null)
        {
            ctx.Logger.LogLine($"boot {name} +{ms:F1} ms");
        }
        else
        {
            Console.WriteLine($"boot {name} +{ms:F1} ms");
        }
    }

    public static IDisposable Measure(ILambdaContext? ctx, string name)
    {
        var s = Stopwatch.StartNew();
        return new Scope(() =>
        {
            s.Stop();
            var line = $"boot {name} took {s.Elapsed.TotalMilliseconds:F1} ms";
            if (ctx != null)
            {
                ctx.Logger.LogLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        });
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        private readonly Action _a = onDispose;
        public void Dispose() => _a();
    }
}
