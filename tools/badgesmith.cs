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
