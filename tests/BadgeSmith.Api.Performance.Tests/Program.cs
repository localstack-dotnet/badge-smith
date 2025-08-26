namespace BadgeSmith.Api.Performance.Tests;

internal static class Program
{
    public static void Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length > 0 && string.Equals(args[0], "--benchmark", StringComparison.OrdinalIgnoreCase))
        {
            BenchmarkDotNet.Running.BenchmarkRunner.Run<RoutingBenchmarks>();
        }
        else
        {
            Console.WriteLine("Run with --benchmark to execute performance benchmarks");
            Console.WriteLine("Example: dotnet run --project tests/BadgeSmith.Api.Tests --configuration Release -- --benchmark");
        }
    }
}
