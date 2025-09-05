using System.Globalization;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.SecretsManager;
using BadgeSmith.DynamoDb.Seeders;
using LocalStack.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static System.Environment;

var builder = Host.CreateApplicationBuilder();

builder.Services
    .AddLocalStack(builder.Configuration)
    .AddDefaultAWSOptions(builder.Configuration.GetAWSOptions())
    .AddAwsService<IAmazonDynamoDB>()
    .AddAwsService<IAmazonSecretsManager>();

builder.Services.AddHostedService<OrgSecretSeeder>();

builder.Services.Configure<HostOptions>(options =>
{
    var timeOutEnv = GetEnvironmentVariable("WORKER_TIMEOUT_IN_SECONDS");

    if (string.IsNullOrEmpty(timeOutEnv) || !int.TryParse(timeOutEnv, CultureInfo.InvariantCulture, out var timeOut))
    {
        throw new InvalidOperationException("WORKER_TIMEOUT_IN_SECONDS environment variable is not set or invalid.");
    }

    options.ServicesStartConcurrently = false;
    options.ServicesStopConcurrently = false;
    options.ShutdownTimeout = TimeSpan.FromSeconds(timeOut);
});

await builder.Build().RunAsync().ConfigureAwait(false);

namespace BadgeSmith.DynamoDb.Seeders
{
    internal sealed record SecretConfig(
        [property: JsonPropertyName("secrets")] SecretInfo[] Secrets);

    internal sealed record SecretInfo(
        [property: JsonPropertyName("org_name")] string OrgName,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("secret")] string Secret,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("description")] string Description);

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(SecretConfig))]
    [JsonSerializable(typeof(SecretInfo))]
    internal sealed partial class SeederJsonSerializerContext : JsonSerializerContext;
}
