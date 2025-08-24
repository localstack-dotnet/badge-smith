using static System.Environment;

namespace BadgeSmith.Api.Observability;

internal static class ObservabilitySettings
{
    private static readonly string? DotNetEnvironmentFromEnv = GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    private const string DefaultAppName = "badge-smith-api";
    private static readonly string DefaultAppVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    private const string DefaultDotNetEnvironment = "Production";
    private const bool DefaultEnableOtel = true;

    private static string? _applicationName;
    private static string? _applicationVersion;
    private static string? _dotNetEnvironment;
    private static bool? _enableOtel;

    public static string ApplicationName => _applicationName ??= GetEnvironmentVariable("APP_NAME") ?? DefaultAppName;
    public static string ApplicationVersion => _applicationVersion ??= GetEnvironmentVariable("APP_VERSION") ?? DefaultAppVersion;
    public static bool EnableOtel => _enableOtel ??= ParseEnvironmentVariable("APP_ENABLE_OTEL") ?? DefaultEnableOtel;
    public static string DotNetEnvironment => _dotNetEnvironment ??= DotNetEnvironmentFromEnv ?? DefaultDotNetEnvironment;

    private static bool? ParseEnvironmentVariable(string name)
    {
        var parsed = bool.TryParse(GetEnvironmentVariable(name), out var boolVal);

        return parsed ? boolVal : null;
    }
}
