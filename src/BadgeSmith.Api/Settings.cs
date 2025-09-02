using static System.Environment;
using static BadgeSmith.Constants;

namespace BadgeSmith.Api;

internal static class Settings
{
    private static readonly string? DotNetEnvironmentFromEnv = GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    private const string DefaultAppName = LambdaName;
    private static readonly string DefaultAppVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    private const string DefaultDotNetEnvironment = "Production";
    private const bool DefaulEnableTelemetryFactoryPerfLogs = true;
    private const bool DefaultUseLocalStack = true;

    private static string? _applicationName;
    private static string? _applicationVersion;
    private static string? _dotNetEnvironment;
    private static bool? _enableTelemetryFactoryPerfLogs;
    private static bool? _useLocalStack;

    public static string ApplicationName => _applicationName ??= GetEnvironmentVariable("APP_NAME") ?? DefaultAppName;

    public static string ApplicationVersion => _applicationVersion ??= GetEnvironmentVariable("APP_VERSION") ?? DefaultAppVersion;

    public static bool TelemetryFactoryPerfLogs =>
        _enableTelemetryFactoryPerfLogs ??= ParseEnvironmentVariable("APP_ENABLE_TELEMETRY_FACTORY_PERF_LOGS") ?? DefaulEnableTelemetryFactoryPerfLogs;

    public static string DotNetEnvironment => _dotNetEnvironment ??= DotNetEnvironmentFromEnv ?? DefaultDotNetEnvironment;

    public static bool UseLocalStack => _useLocalStack ??= ParseEnvironmentVariable("LocalStack__UseLocalStack") ?? DefaultUseLocalStack;

    public static string? LocalStackEndpoint => GetEnvironmentVariable("ConnectionStrings__localstack");

#if !DEBUG
    public static TimeSpan LambdaTimeout => TimeSpan.FromSeconds(Constants.LambdaTimeoutInSeconds).Subtract(TimeSpan.FromSeconds(2));
#else
    public static TimeSpan LambdaTimeout => TimeSpan.FromMinutes(3);
#endif

    private static bool? ParseEnvironmentVariable(string name)
    {
        var parsed = bool.TryParse(GetEnvironmentVariable(name), out var boolVal);

        return parsed ? boolVal : null;
    }
}
