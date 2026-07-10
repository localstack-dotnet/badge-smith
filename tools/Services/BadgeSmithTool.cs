using BadgeSmith.Tools.Commands;
using BadgeSmith.Tools.Infrastructure;
using BadgeSmith.Tools.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Cli;
using HostFactory = Microsoft.Extensions.Hosting.Host;

namespace BadgeSmith.Tools;

internal static class BadgeSmithTool
{
    public static async Task<int> RunAsync(string[] args, Action<HostApplicationBuilder>? configureHost = null, IAnsiConsole? console = null)
    {
        ArgumentNullException.ThrowIfNull(args);

        var selectedConsole = console ?? AnsiConsole.Console;
        var builder = HostFactory.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = [],
        });

        builder.Services.AddBadgeSmithToolServices(builder.Configuration, selectedConsole);
        configureHost?.Invoke(builder);

        var registrar = new HostTypeRegistrar(builder.Services);
        var app = CreateCommandApp(builder.Services, registrar);
        using var host = builder.Build();
        registrar.UseServiceProvider(host.Services);

        return await app.RunAsync(args).ConfigureAwait(false);
    }

    public static CommandApp CreateCommandApp(IServiceCollection services, HostTypeRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registrar);

        var console = services.FirstOrDefault(static descriptor => descriptor.ServiceType == typeof(IAnsiConsole))?.ImplementationInstance as IAnsiConsole
            ?? AnsiConsole.Console;
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName("badgesmith");
            config.Settings.Console = console;
            config.Settings.ShowOptionDefaultValues = true;
            config.Settings.CaseSensitivity = CaseSensitivity.None;
            config.Settings.CancellationExitCode = ToolExitCodes.Canceled;
            config.AddBranch("lambda", lambda =>
            {
                lambda.SetDescription("Lambda build and artifact commands.");
                lambda.AddCommand<LambdaBuildCommand>("build")
                    .WithDescription("Build the BadgeSmith Lambda ZIP or container image.")
                    .WithExample("lambda", "build", "--target", "zip", "--rid", "linux-arm64", "--clean");
            });
            config.AddBranch("tests", tests =>
            {
                tests.SetDescription("Test execution and ingestion commands.");
                tests.AddCommand<TestRunCommand>("run")
                    .WithDescription("Run a .NET test project once per target framework.")
                    .WithExample("tests", "run", "--project-path", "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj", "--results-dir", "test-results");
                tests.AddCommand<TestIngestCommand>("ingest")
                    .WithDescription("Post a test result payload to BadgeSmith.")
                    .WithExample("tests", "ingest", "--base-url", "https://api.example.com", "--owner", "localstack-dotnet", "--repo", "badge-smith", "--platform", "linux", "--branch", "main", "--secret", "secret", "--payload-file", "payload.json", "--dry-run");
            });
            config.AddBranch("secrets", secrets =>
            {
                secrets.SetDescription("Secrets Manager and org mapping commands.");
                secrets.AddCommand<SecretsSeedCommand>("seed")
                    .WithDescription("Seed GitHub org secret mappings into AWS resources.")
                    .WithExample("secrets", "seed", "--config", "tools/organization-pat-mapping.json", "--table-name", "badge-smith-github-org-secrets", "--dry-run");
            });
            config.AddBranch("badge", badge =>
            {
                badge.SetDescription("BadgeSmith badge update commands.");
                badge.AddCommand<BadgeUpdateCommand>("update")
                    .WithDescription("Post GitHub Actions test results to BadgeSmith.")
                    .WithExample("badge", "update", "--base-url", "https://badges.example.com", "--platform", "Linux", "--test-passed", "1", "--test-failed", "0", "--test-skipped", "0", "--repository", "localstack-dotnet/badge-smith", "--dry-run");
            });
            config.SetExceptionHandler((exception, _) =>
            {
                if (exception is OperationCanceledException)
                {
                    console.MarkupLine("[yellow]Operation canceled.[/]");
                    return ToolExitCodes.Canceled;
                }

                console.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
                return ToolExitCodes.GeneralFailure;
            });
        });

        return app;
    }
}
