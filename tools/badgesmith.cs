#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:package Spectre.Console.Cli
#:package CliWrap
#:package AWSSDK.DynamoDBv2
#:package AWSSDK.SecretsManager
#:include Commands/**/*.cs
#:include Infrastructure/**/*.cs

using BadgeSmith.Tools.Commands;
using BadgeSmith.Tools.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("badgesmith");
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
    });
    config.SetExceptionHandler((exception, _) =>
    {
        if (exception is OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Operation canceled.[/]");
            return ToolExitCodes.Canceled;
        }

        AnsiConsole.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
        return ToolExitCodes.GeneralFailure;
    });
});

return await app.RunAsync(args).ConfigureAwait(false);
