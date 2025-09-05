#pragma warning disable CA1812, MA0134, VSTHRD110, CA2000

using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Environment;

namespace BadgeSmith.DynamoDb.Seeders;

internal sealed class OrgSecretSeeder : IHostedService
{
    private int? _exitCode;

    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly ILogger<OrgSecretSeeder> _logger;
    private readonly HostOptions _hostOptions;
    private readonly IHostApplicationLifetime _appLifetime;

    public OrgSecretSeeder(
        IAmazonDynamoDB dynamoDb,
        IAmazonSecretsManager secretsManager,
        ILogger<OrgSecretSeeder> logger,
        IOptions<HostOptions> hostOptions,
        IHostApplicationLifetime appLifetime)
    {
        _dynamoDb = dynamoDb;
        _secretsManager = secretsManager;
        _logger = logger;
        _hostOptions = hostOptions.Value;
        _appLifetime = appLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? cts = null;
        CancellationTokenSource? linkedCts;
        if (_hostOptions.StartupTimeout != Timeout.InfiniteTimeSpan)
        {
            cts = new CancellationTokenSource(_hostOptions.StartupTimeout);
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken, _appLifetime.ApplicationStopping);
        }
        else
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);
        }

        Task.Run(async () =>
        {
            _logger.LogInformation("OrgSecretSeeder is in start state");

            try
            {
                var configuration = await LoadConfigurationAsync(linkedCts.Token).ConfigureAwait(false);
                if (configuration?.Secrets is null or { Length: 0 })
                {
                    return;
                }

                var tableName = GetEnvironmentVariable("AWS_RESOURCE_ORG_SECRETS_TABLE");
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    _logger.LogWarning("AWS_RESOURCE_ORG_SECRETS_TABLE environment variable not set, skipping seeder");
                    return;
                }

                await SeedOrganizationsAsync(tableName, configuration.Secrets, linkedCts.Token).ConfigureAwait(false);

                _exitCode = 0;
                _logger.LogInformation("Work completed successfully, stopping the application");
            }
            catch (OperationCanceledException ex)
            {
                _exitCode = 1;
                _logger.LogWarning(ex, "Work was canceled or timed out, stopping the application");
            }
            catch (Exception ex)
            {
                _exitCode = 1;
                _logger.LogError(ex, "An error occurred during work execution");
            }
            finally
            {
                cts?.Dispose();
                linkedCts?.Dispose();
                _appLifetime.StopApplication();
            }
        }, linkedCts.Token);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Exiting with return code: {ExitCode}", _exitCode);

        // Exit code may be null if the user canceled via Ctrl+C/SIGTERM
        ExitCode = _exitCode ?? -1;
        return Task.CompletedTask;
    }

    private async Task<SecretConfig?> LoadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        const string configPath = "organization-pat-mapping.json";

        if (!File.Exists(configPath))
        {
            _logger.LogInformation("Config file not found at {ConfigPath}, skipping seeder", configPath);
            return null;
        }

        try
        {
            var configJson = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<SecretConfig>(configJson, SeederJsonSerializerContext.Default.SecretConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read or parse config file at {ConfigPath}", configPath);
            return null;
        }
    }

    private async Task SeedOrganizationsAsync(string tableName, SecretInfo[] organizations, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing {Count} organizations from config", organizations.Length);

        foreach (var orgConfig in organizations)
        {
            if (string.IsNullOrWhiteSpace(orgConfig.Name) || string.IsNullOrWhiteSpace(orgConfig.Secret))
            {
                _logger.LogWarning("Skipping organization with missing name or token");
                continue;
            }

            var orgLower = orgConfig.OrgName.ToLowerInvariant();
            var secretType = orgConfig.Type.ToLowerInvariant();
            var keyName = orgConfig.Name.ToLowerInvariant();
            var secretName = $"badgesmith/github/{keyName}";

            try
            {
                await CreateOrUpdateSecretAsync(secretName, orgConfig.Secret, orgLower, cancellationToken).ConfigureAwait(false);
                await CreateDynamoDbMappingAsync(tableName, orgLower, secretType, secretName, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Seeded org mapping for {Org}", keyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed organization {Org}", keyName);
            }
        }
    }

    private async Task CreateOrUpdateSecretAsync(string secretName, string token, string orgLower, CancellationToken cancellationToken = default)
    {
        try
        {
            await _secretsManager.CreateSecretAsync(new CreateSecretRequest
            {
                Name = secretName,
                SecretString = token,
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created secret {SecretName} for org {Org}", secretName, orgLower);
        }
        catch (ResourceExistsException ex)
        {
            await _secretsManager.PutSecretValueAsync(new PutSecretValueRequest
            {
                SecretId = secretName,
                SecretString = token,
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(ex, "Updated secret {SecretName} for org {Org}", secretName, orgLower);
        }
    }

    private async Task CreateDynamoDbMappingAsync(string tableName, string orgLower, string secretType, string secretName, CancellationToken cancellationToken = default)
    {
        var item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
        {
            ["PK"] = new($"ORG#{orgLower}"),
            ["SK"] = new($"CONST#GITHUB#{secretType}"),
            ["SecretName"] = new(secretName),
            ["CreatedAt"] = new(DateTime.UtcNow.ToString("O")),
        };

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = tableName,
            Item = item,
        }, cancellationToken).ConfigureAwait(false);
    }
}
