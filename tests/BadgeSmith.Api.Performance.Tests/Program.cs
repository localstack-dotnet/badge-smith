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
            _ => typeof(BufferAllocationBenchmarks), // Default to buffer benchmarks
        };
    }

    private static void PrintUsage()
    {
        Console.WriteLine("BadgeSmith Performance Benchmarks");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --configuration Release -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --type=<buffer|routing>     Choose benchmark suite (default: buffer)");
        Console.WriteLine("  --category=<category>       Filter by benchmark category");
        Console.WriteLine("  --mode=<dry|short|memory>   Benchmark execution mode");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Quick buffer allocation tests");
        Console.WriteLine("  dotnet run -c Release -- --type=buffer --category=Quick");
        Console.WriteLine();
        Console.WriteLine("  # All buffer benchmarks with memory focus");
        Console.WriteLine("  dotnet run -c Release -- --type=buffer --mode=memory");
        Console.WriteLine();
        Console.WriteLine("  # Fast validation run");
        Console.WriteLine("  dotnet run -c Release -- --type=buffer --mode=dry");
        Console.WriteLine();
        Console.WriteLine("  # Original routing benchmarks");
        Console.WriteLine("  dotnet run -c Release -- --type=routing");
    }
}
