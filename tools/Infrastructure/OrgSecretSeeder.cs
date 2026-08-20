using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Spectre.Console;

namespace BadgeSmith.Tools.Infrastructure;

internal sealed class OrgSecretSeeder
{
    private readonly IAnsiConsole _console;

    public OrgSecretSeeder(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public async Task<int> SeedAsync(
        string configPath,
        string tableName,
        IAmazonDynamoDB? dynamoDb,
        IAmazonSecretsManager? secretsManager,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigurationAsync(configPath, cancellationToken).ConfigureAwait(false);
        if (config.Secrets.Length == 0)
        {
            _console.MarkupLine("[yellow]No secrets found in config.[/]");
            return ToolExitCodes.Success;
        }

        var normalizedSecrets = new NormalizedSecret[config.Secrets.Length];
        for (var i = 0; i < config.Secrets.Length; i++)
        {
            normalizedSecrets[i] = Normalize(config.Secrets[i]);
        }

        if (dryRun)
        {
            foreach (var normalized in normalizedSecrets)
            {
                _console.MarkupLine("[yellow]DRY RUN: org secret would be seeded.[/]");
                _console.WriteLine($"PK: ORG#{normalized.OrgName}");
                _console.WriteLine($"SK: CONST#GITHUB#{normalized.Type}");
                _console.WriteLine($"SecretName: {normalized.SecretName}");
            }

            return ToolExitCodes.Success;
        }

        if (dynamoDb is null || secretsManager is null)
        {
            throw new InvalidOperationException("AWS clients are required when --dry-run is not set.");
        }

        foreach (var normalized in normalizedSecrets)
        {
            await CreateOrUpdateSecretAsync(secretsManager, normalized.SecretName, normalized.Value, cancellationToken).ConfigureAwait(false);
            await PutMappingAsync(dynamoDb, tableName, normalized.OrgName, normalized.Type, normalized.SecretName, cancellationToken).ConfigureAwait(false);
            _console.MarkupLine($"[green]Seeded org mapping for {Markup.Escape(normalized.OrgName)} / {Markup.Escape(normalized.Type)}.[/]");
        }

        return ToolExitCodes.Success;
    }

    private static async Task<SecretConfig> LoadConfigurationAsync(string configPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Secret mapping config file was not found.", configPath);
        }

        var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, OrgSecretSeederJsonContext.Default.SecretConfig)
            ?? new SecretConfig([]);
    }

    private static NormalizedSecret Normalize(SecretInfo secret)
    {
        if (string.IsNullOrWhiteSpace(secret.OrgName))
        {
            throw new InvalidOperationException("Secret entry is missing org_name.");
        }

        if (string.IsNullOrWhiteSpace(secret.Name))
        {
            throw new InvalidOperationException("Secret entry is missing name.");
        }

        if (string.IsNullOrWhiteSpace(secret.Secret))
        {
            throw new InvalidOperationException($"Secret entry '{secret.Name}' is missing secret.");
        }

        if (string.IsNullOrWhiteSpace(secret.Type))
        {
            throw new InvalidOperationException($"Secret entry '{secret.Name}' is missing type.");
        }

        var orgName = secret.OrgName.Trim().ToLowerInvariant();
        var type = secret.Type.Trim().ToLowerInvariant();
        var keyName = secret.Name.Trim().ToLowerInvariant();
        var secretName = $"badgesmith/github/{orgName}/{keyName}";
        if (!IsValidSecretName(secretName))
        {
            throw new InvalidOperationException(
                $"Secret entry '{secret.Name}' produces an invalid AWS Secrets Manager name.");
        }

        return new NormalizedSecret(orgName, type, secretName, secret.Secret);
    }

    private static bool IsValidSecretName(ReadOnlySpan<char> value)
    {
        if (value.Length is < 1 or > 512)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('/' or '_' or '+' or '=' or '.' or '@' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task CreateOrUpdateSecretAsync(IAmazonSecretsManager secretsManager, string secretName, string value, CancellationToken cancellationToken)
    {
        try
        {
            await secretsManager.CreateSecretAsync(new CreateSecretRequest
            {
                Name = secretName,
                SecretString = value,
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ResourceExistsException)
        {
            await secretsManager.PutSecretValueAsync(new PutSecretValueRequest
            {
                SecretId = secretName,
                SecretString = value,
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task<PutItemResponse> PutMappingAsync(IAmazonDynamoDB dynamoDb, string tableName, string orgName, string type, string secretName, CancellationToken cancellationToken)
    {
        return dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = tableName,
            Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new($"ORG#{orgName}"),
                ["SK"] = new($"CONST#GITHUB#{type}"),
                ["SecretName"] = new(secretName),
                ["CreatedAt"] = new(DateTime.UtcNow.ToString("O")),
            },
        }, cancellationToken);
    }

    private sealed record NormalizedSecret(string OrgName, string Type, string SecretName, string Value);
}

internal sealed record SecretConfig([property: JsonPropertyName("secrets")] SecretInfo[] Secrets);

internal sealed record SecretInfo(
    [property: JsonPropertyName("org_name")] string OrgName,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("secret")] string Secret,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description);

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SecretConfig))]
[JsonSerializable(typeof(SecretInfo))]
internal sealed partial class OrgSecretSeederJsonContext : JsonSerializerContext;
