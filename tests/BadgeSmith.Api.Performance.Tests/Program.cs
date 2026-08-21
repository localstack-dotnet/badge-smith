using BenchmarkDotNet.Running;

namespace BadgeSmith.Api.Performance.Tests;

internal static class Program
{
    public static void Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var benchmarkType = GetBenchmarkType(args);
        BenchmarkRunner.Run(benchmarkType);
    }

    private static Type GetBenchmarkType(string[] args)
    {
        var typeArg = Array.Find(args, arg => arg.StartsWith("--type=", StringComparison.OrdinalIgnoreCase));

        return typeArg?.Split('=')[1].ToLowerInvariant() switch
        {
            "buffer" => typeof(BufferAllocationBenchmarks),
            "routing" => typeof(RoutingBenchmarks),
            "providers" => typeof(ProviderBenchmarks),
            _ => typeof(BufferAllocationBenchmarks), // Default to buffer benchmarks
        };
    }

    private static void PrintUsage()
    {
        Console.WriteLine("BadgeSmith Performance Benchmarks");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --configuration Release -- --type=<suite>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --type=<buffer|routing|providers>  Choose benchmark suite (default: buffer)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Buffer allocation tests");
        Console.WriteLine("  dotnet run -c Release -- --type=buffer");
        Console.WriteLine();
        Console.WriteLine("  # Routing benchmarks");
        Console.WriteLine("  dotnet run -c Release -- --type=routing");
        Console.WriteLine();
        Console.WriteLine("  # Provider upstream fetch allocation profile");
        Console.WriteLine("  dotnet run -c Release -- --type=providers");
    }
}
