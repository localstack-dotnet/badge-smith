using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BadgeSmith.Tools.Configuration;

internal interface IAwsCommandSettings
{
    internal string? AwsProfile { get; }

    internal string? AwsRegion { get; }

    internal bool LocalStack { get; }

    internal bool NoLocalStack { get; }
}

internal abstract class AwsCommandSettings : CommandSettings, IAwsCommandSettings
{
    [CommandOption("--aws-profile")]
    [Description("AWS profile to use when LocalStack is disabled.")]
    public string? AwsProfile { get; init; }

    [CommandOption("--aws-region")]
    [Description("AWS region to use for AWS SDK clients.")]
    public string? AwsRegion { get; init; }

    [CommandOption("--localstack")]
    [Description("Use LocalStack-backed AWS SDK clients.")]
    public bool LocalStack { get; init; }

    [CommandOption("--no-localstack")]
    [Description("Disable LocalStack even when enabled by configuration.")]
    public bool NoLocalStack { get; init; }

    protected ValidationResult ValidateAwsSettings()
    {
        return LocalStack && NoLocalStack
            ? ValidationResult.Error("--localstack and --no-localstack cannot be used together.")
            : ValidationResult.Success();
    }
}
