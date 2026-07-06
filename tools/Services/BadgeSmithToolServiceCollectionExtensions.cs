using BadgeSmith.Tools.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace BadgeSmith.Tools.Services;

internal static class BadgeSmithToolServiceCollectionExtensions
{
    public static IServiceCollection AddBadgeSmithToolServices(this IServiceCollection services, IConfiguration configuration, IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(console);

        services.AddSingleton(console);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IToolLogger, SpectreConsoleLogger>();
        services.AddSingleton<RepositoryPaths>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IAwsOptionsResolver, AwsOptionsResolver>();
        services.AddSingleton<IToolAwsClientFactory, ToolAwsClientFactory>();
        services.AddSingleton<OrgSecretSeeder>();
        services.AddHttpClient("badgesmith-api");

        return services;
    }
}
