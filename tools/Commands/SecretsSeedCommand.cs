using BadgeSmith.Tools.Infrastructure;
using BadgeSmith.Tools.Configuration;
using BadgeSmith.Tools.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace BadgeSmith.Tools.Commands;

internal sealed class SecretsSeedCommand : AsyncCommand<SecretsSeedSettings>
{
    private readonly RepositoryPaths _paths;
    private readonly OrgSecretSeeder _seeder;
    private readonly IAwsOptionsResolver _awsOptionsResolver;
    private readonly IToolAwsClientFactory _awsClientFactory;
    private readonly IAnsiConsole _console;

    public SecretsSeedCommand(
        RepositoryPaths paths,
        OrgSecretSeeder seeder,
        IAwsOptionsResolver awsOptionsResolver,
        IToolAwsClientFactory awsClientFactory,
        IAnsiConsole console)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _seeder = seeder ?? throw new ArgumentNullException(nameof(seeder));
        _awsOptionsResolver = awsOptionsResolver ?? throw new ArgumentNullException(nameof(awsOptionsResolver));
        _awsClientFactory = awsClientFactory ?? throw new ArgumentNullException(nameof(awsClientFactory));
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, SecretsSeedSettings settings, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var configPath = Path.IsPathRooted(settings.Config)
            ? settings.Config
            : _paths.ResolveFromRoot(settings.Config);

        if (!File.Exists(configPath))
        {
            _console.MarkupLine($"[red]Secret mapping config file was not found: {Markup.Escape(configPath)}[/]");
            _console.MarkupLine("[yellow]Copy tools/organization-pat-mapping.json.dist to tools/organization-pat-mapping.json and fill in local secrets.[/]");
            return ToolExitCodes.ValidationFailure;
        }

        var tableName = settings.TableName;
        if (string.IsNullOrWhiteSpace(tableName))
        {
            tableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_ORG_SECRETS_TABLE");
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            _console.MarkupLine("[red]Org secrets table name is required. Use --table-name or AWS_RESOURCE_ORG_SECRETS_TABLE.[/]");
            return ToolExitCodes.ValidationFailure;
        }

        if (settings.DryRun)
        {
            return await _seeder.SeedAsync(configPath, tableName, dynamoDb: null, secretsManager: null, dryRun: true, cts.Token).ConfigureAwait(false);
        }

        var awsOptions = _awsOptionsResolver.Resolve(settings);
        using var awsClients = _awsClientFactory.Create(awsOptions);
        return await _seeder.SeedAsync(configPath, tableName, awsClients.DynamoDb, awsClients.SecretsManager, dryRun: false, cts.Token).ConfigureAwait(false);
    }
}

internal sealed class SecretsSeedSettings : AwsCommandSettings
{
    [CommandOption("--config")]
    [Description("Path to organization PAT mapping JSON, relative to the repository root unless absolute.")]
    public string Config { get; init; } = "tools/organization-pat-mapping.json";

    [CommandOption("--table-name")]
    [Description("DynamoDB org secrets table name. Defaults to AWS_RESOURCE_ORG_SECRETS_TABLE.")]
    public string? TableName { get; init; }

    [CommandOption("--timeout-seconds")]
    [Description("Maximum seed duration in seconds.")]
    public int TimeoutSeconds { get; init; } = 300;

    [CommandOption("--dry-run")]
    [Description("Validate and display planned changes without writing to AWS.")]
    public bool DryRun { get; init; }

    public override ValidationResult Validate()
    {
        var awsValidation = ValidateAwsSettings();
        if (!awsValidation.Successful)
        {
            return awsValidation;
        }

        if (TimeoutSeconds <= 0)
        {
            return ValidationResult.Error("--timeout-seconds must be greater than zero.");
        }

        return ValidationResult.Success();
    }
}
