using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Aspire.Hosting;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.Testing;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit;
using static BadgeSmith.Constants;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

/// <summary>
/// The collection name is intentionally "aspire-contract" so functional contract
/// tests serialize against this single fixture (DisableParallelization = true).
/// </summary>
[CollectionDefinition("aspire-contract", DisableParallelization = true)]
public sealed class AspireContractFixtureRegistration : ICollectionFixture<AspireContractFixture>;

public sealed class AspireContractFixture : IAsyncLifetime
{
    private const string Region = "eu-central-1";
    private const string TelemetryStubId = "908d95fe-8260-4321-a97d-55b57c5d0f8e";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication? _app;
    private IContainer? _wiremock;

    public ContractHttpClient Api { get; private set; } = null!;
    public IAmazonDynamoDB DynamoDb { get; private set; } = null!;
    public IAmazonSecretsManager Secrets { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
        Environment.SetEnvironmentVariable(UpstreamModeEnvironmentVariable, UpstreamModeMock);

        var wiremockDir = Path.Combine(AppContext.BaseDirectory, "Testing", "Infrastructure", "wiremock");
        _wiremock = new ContainerBuilder("wiremock/wiremock:3.9.1")
            .WithBindMount(wiremockDir, "/home/wiremock", AccessMode.ReadOnly)
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPort(8080).ForPath("/__admin/health")))
            .Build();

        await _wiremock.StartAsync().ConfigureAwait(false);
        var wiremockBaseUrl = $"http://{_wiremock.Hostname}:{_wiremock.GetMappedPublicPort(8080)}";

        // Test-owned WireMock upstreams override the production NuGet/GitHub base URLs.
        // These must be set before the Aspire AppHost builder is created so that the
        // Lambda emulator receives the resolved values via the AppHost's env pass-through.
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", wiremockBaseUrl + "/nuget/");
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", wiremockBaseUrl + "/github/");

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.BadgeSmith_Host>()
            .ConfigureAwait(false);

        // Testing runs without the dashboard. Keep Lambda's telemetry flush enabled,
        // but give its exporter a test-owned endpoint instead of the dashboard default.
        var lambda = builder.Resources.OfType<LambdaProjectResource>().Single();
        builder.CreateResourceBuilder(lambda)
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", wiremockBaseUrl + "/otlp")
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");

        _app = await builder.BuildAsync().ConfigureAwait(false);

        using var startupCts = new CancellationTokenSource(StartupTimeout);
        await _app.StartAsync(startupCts.Token).ConfigureAwait(false);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("APIGatewayEmulator", startupCts.Token)
            .ConfigureAwait(false);

        var apiEndpoint = _app.GetEndpoint("APIGatewayEmulator", "http");
        Api = new ContractHttpClient(apiEndpoint);

        var localStackEndpoint = _app.GetEndpoint("localstack", "http");
        var credentials = new BasicAWSCredentials("test", "test");
        DynamoDb = new AmazonDynamoDBClient(credentials, new AmazonDynamoDBConfig
        {
            ServiceURL = localStackEndpoint.ToString().TrimEnd('/'),
            AuthenticationRegion = Region,
        });
        Secrets = new AmazonSecretsManagerClient(credentials, new AmazonSecretsManagerConfig
        {
            ServiceURL = localStackEndpoint.ToString().TrimEnd('/'),
            AuthenticationRegion = Region,
        });

        await AwsTestSeeder.CreateTablesAndSecretsAsync(DynamoDb, Secrets).ConfigureAwait(false);
    }

    public async Task<int> CountTelemetryExportsAsync(CancellationToken ct)
    {
        var wiremock = _wiremock ?? throw new InvalidOperationException("The contract fixture has not started.");
        using var client = new HttpClient();
        var endpoint = new Uri($"http://{wiremock.Hostname}:{wiremock.GetMappedPublicPort(8080)}/__admin/requests?matchingStub={TelemetryStubId}");
        using var response = await client.GetAsync(endpoint, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var count = 0;
        foreach (var export in document.RootElement.GetProperty("requests").EnumerateArray())
        {
            if (export.GetProperty("wasMatched").GetBoolean() && export.GetProperty("responseDefinition").GetProperty("status").GetInt32() == 200)
            {
                count++;
            }
        }

        return count;
    }

    public async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", null);
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", null);
        Environment.SetEnvironmentVariable(UpstreamModeEnvironmentVariable, null);

        DynamoDb?.Dispose();
        Secrets?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_wiremock is not null)
        {
            await _wiremock.DisposeAsync().ConfigureAwait(false);
        }
    }
}
