using BenchmarkDotNet.Running;

namespace BadgeSmith.Api.Performance.Tests;

internal static class Program
{
    public static void Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args.Length == 0 ? ["--help"] : args);
    }
}
