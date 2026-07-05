# Iteration 0 — AOT Contract Tier, Baseline Harness, Multi-Arch Build: Implementation Plan

> Superseded note (2026-07-04): Do not continue this RIE-based task list. Use `2026-07-04-rie-free-aspire-localstack-implementation-plan.md` for implementation.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A contract-test tier that exercises the real published Native AOT artifact in the real Lambda base image, a repeatable perf/memory baseline harness, and a QEMU-free multi-arch Docker build — per the approved spec `docs/plans/2026-07-02-iteration0-aot-contract-tier-design.md`.

**Architecture:** The existing `tests/BadgeSmith.Api.Tests` project becomes the single test host for unit, integration, functional, Aspire-local, and AOT-contract tests. Reusable Testcontainers infrastructure under `Testing/Infrastructure` boots LocalStack + WireMock + the prebuilt `badge-smith` image (RIE) on a shared Docker network; functional AOT-contract tests POST API Gateway v2 events to the RIE invocation endpoint. A single-source `perf-baseline.sh` assembles the same stack via docker CLI and records dated JSON results. The Dockerfile build stage runs on `$BUILDPLATFORM` and cross-links arm64.

**Tech Stack:** xUnit v3 (VSTest), Testcontainers + Testcontainers.LocalStack, WireMock 3 (container), AWS SDK v4, k6, Docker Buildx, GitHub Actions (`ubuntu-24.04-arm`).

## Global Constraints

- Zero build warnings (`TreatWarningsAsErrors=true` repo-wide); AOT/trim warnings are blocking.
- Package versions ONLY via `dotnet add package` (Central Package Management) — never hand-edit csproj/props versions.
- No DI container, no config framework; env vars only. UTC only (`DateTime.Now` banned).
- Every serialized type must be in `LambdaFunctionJsonSerializerContext` (API side; test code may use its own reflection JSON — tests run JIT).
- All tests live in `tests/BadgeSmith.Api.Tests` and carry explicit xUnit traits: `Category=Unit`, `Category=Integration`, `Category=Functional`, and/or `Category=AotContract`. A test may carry multiple `Category` traits when it legitimately belongs to multiple layers; AOT contract tests are `Integration` + `Functional` + `AotContract`.
- After every task: `slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"` must pass (baseline committed at `6c6f609`).
- Commits: Conventional Commits, no AI attribution trailers. Commits inside this plan are pre-approved by plan approval, EXCEPT Task 12 (CI workflows) which requires a fresh explicit approval before execution (AGENTS.md CI gate).
- Contract tests pin CURRENT behavior including known bugs (e.g., malformed hex signature → 500). Do not fix production bugs in this plan.
- Required capabilities per spec: load `dotnet-skills:testcontainers`, `dotnet-skills:project-structure`, `dotnet-skills:package-management`, `dotnet-test:run-tests` for test-project tasks; `dotnet-diag:microbenchmarking` for Task 9–10; `dotnet-skills:serialization` if touching JSON shapes; process skills `superpowers:test-driven-development`, `superpowers:verification-before-completion`.
- All commands below run from the repo root `E:\repos\my-projects\badge-smith` (Git Bash syntax; on Windows PowerShell use equivalent).

## Execution Routing

Use subagent-driven development. Task 4 must use `glm-hardcore` because it establishes the reusable testing infrastructure and taxonomy. Tasks 1–2, 5–7, 9–10 use `codex-coder`; Task 11 uses `glm-hardcore`; Tasks 3, 8, and 13 use `deepseek-coder`; every task review uses `codex-review`. Task 12 remains a hard approval stop before any CI workflow edit.

---

### Task 1: Spike — prod image boots under RIE and answers /health

**Files:**
- Create: none (throwaway commands; findings recorded in Task 13 README)

**Interfaces:**
- Produces: verified knowledge — image tag `badge-smith:local` (amd64), RIE on container port 8080, invocation path `/2015-03-31/functions/function/invocations`.

- [ ] **Step 1: Build the amd64 prod image (existing Dockerfile stage)**

Run: `docker build --target lambda-image -t badge-smith:local .`
Expected: success (~5–10 min first time). `docker image inspect badge-smith:local --format '{{.Architecture}}'` → `amd64`.

- [ ] **Step 2: Run it and invoke /health**

```bash
docker run -d --name bs-spike -p 9000:8080 \
  -e DOTNET_ENVIRONMENT=Production \
  -e AWS_RESOURCE_TEST_RESULTS_TABLE=badge-smith-test-result \
  -e AWS_RESOURCE_NONCE_TABLE=badge-smith-hmac-nonce \
  -e AWS_RESOURCE_ORG_SECRETS_TABLE=badge-smith-github-org-secrets \
  badge-smith:local
curl -s -XPOST http://localhost:9000/2015-03-31/functions/function/invocations -d '{
  "version":"2.0","routeKey":"$default","rawPath":"/health",
  "headers":{},
  "requestContext":{"http":{"method":"GET","path":"/health"},"stage":"$default","requestId":"spike-1"},
  "isBase64Encoded":false}'
```

Expected: JSON containing `"statusCode":200` and a body with `"status":"Healthy"`. If RIE is missing (connection refused / entrypoint error), record it and apply the spec fallback (add RIE to a test-only image layer) before continuing.

- [ ] **Step 3: Cleanup**

Run: `docker rm -f bs-spike`

---

### Task 2: Spike — AWS SDK v4 honors `AWS_ENDPOINT_URL_*`

**Files:** none (throwaway)

**Interfaces:**
- Produces: verified env-var names the fixture will use: `AWS_ENDPOINT_URL_DYNAMODB`, `AWS_ENDPOINT_URL_SECRETS_MANAGER` (fallback if unsupported: env-based `ServiceURL` override in `AwsClientBuilder` — requires Deniz approval, stop and ask).

- [ ] **Step 1: Boot LocalStack + lambda on one network, point SDK via env**

```bash
docker network create bs-spike-net
docker run -d --name bs-ls --network bs-spike-net --network-alias localstack -p 4566:4566 localstack/localstack:4.6
sleep 10
docker run -d --name bs-spike --network bs-spike-net -p 9000:8080 \
  -e DOTNET_ENVIRONMENT=Production \
  -e AWS_ACCESS_KEY_ID=test -e AWS_SECRET_ACCESS_KEY=test -e AWS_REGION=eu-central-1 \
  -e AWS_ENDPOINT_URL_DYNAMODB=http://localstack:4566 \
  -e AWS_ENDPOINT_URL_SECRETS_MANAGER=http://localstack:4566 \
  -e AWS_RESOURCE_TEST_RESULTS_TABLE=badge-smith-test-result \
  -e AWS_RESOURCE_NONCE_TABLE=badge-smith-hmac-nonce \
  -e AWS_RESOURCE_ORG_SECRETS_TABLE=badge-smith-github-org-secrets \
  badge-smith:local
aws --endpoint-url http://localhost:4566 dynamodb create-table --table-name badge-smith-test-result \
  --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S AttributeName=GSI1PK,AttributeType=S AttributeName=GSI1SK,AttributeType=S \
  --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes 'IndexName=GSI1,KeySchema=[{AttributeName=GSI1PK,KeyType=HASH},{AttributeName=GSI1SK,KeyType=RANGE}],Projection={ProjectionType=ALL}' \
  --billing-mode PAY_PER_REQUEST --region eu-central-1
```

- [ ] **Step 2: Invoke the tests-badge route; expect a DynamoDB-backed 404, not a connection error**

```bash
curl -s -XPOST http://localhost:9000/2015-03-31/functions/function/invocations -d '{
  "version":"2.0","routeKey":"$default","rawPath":"/badges/tests/linux/test-org/test-repo/main",
  "headers":{},
  "requestContext":{"http":{"method":"GET","path":"/badges/tests/linux/test-org/test-repo/main"},"stage":"$default","requestId":"spike-2"},
  "isBase64Encoded":false}'
```

Expected: `"statusCode":404` with body containing `No test results found` → SDK reached LocalStack. A 500 mentioning credentials/connectivity means env vars were ignored → STOP, report, get approval for the `AwsClientBuilder` fallback.

- [ ] **Step 3: Cleanup**

Run: `docker rm -f bs-spike bs-ls && docker network rm bs-spike-net`

---

### Task 3: Upstream base-URL env overrides in `HttpClientFactory` (TDD)

**Files:**
- Modify: `src/BadgeSmith.Api/Core/Http/HttpClientFactory.cs`
- Test: `tests/BadgeSmith.Api.Tests/Http/HttpClientFactoryTests.cs` (new)

**Interfaces:**
- Produces: env vars `HTTP_NUGET_BASE_URL` and `HTTP_GITHUB_BASE_URL` (absolute URI, trailing slash recommended) override the hardcoded base addresses; unset/invalid → current defaults. Consumed by fixture (Task 4) and harness (Task 9).

- [ ] **Step 1: Write the failing test**

```csharp
using BadgeSmith.Api.Core.Http;

namespace BadgeSmith.Api.Tests.Http;

public sealed class HttpClientFactoryTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", null);
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", null);
    }

    [Fact]
    public void CreateNuGetClient_Should_UseDefaultBaseAddress_WhenEnvNotSet()
    {
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("https://api.nuget.org/"), client.BaseAddress);
    }

    [Fact]
    public void CreateNuGetClient_Should_UseEnvOverride_WhenSet()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "http://wiremock:8080/nuget/");
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("http://wiremock:8080/nuget/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_UseEnvOverride_WhenSet()
    {
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", "http://wiremock:8080/github/");
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("http://wiremock:8080/github/"), client.BaseAddress);
    }

    [Fact]
    public void CreateNuGetClient_Should_FallBackToDefault_WhenEnvInvalid()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "not-a-uri");
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("https://api.nuget.org/"), client.BaseAddress);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~HttpClientFactoryTests"`
Expected: FAIL — `UseEnvOverride` tests fail (base address is the hardcoded default).

- [ ] **Step 3: Implement — resolve base URI at client-creation time**

In `HttpClientFactory.cs`, add below the constants:

```csharp
    private static Uri ResolveBaseUri(string envVar, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : new Uri(fallback);
    }
```

Replace `BaseAddress = new Uri(NugetApiUrl)` with `BaseAddress = ResolveBaseUri("HTTP_NUGET_BASE_URL", NugetApiUrl)`, and `BaseAddress = new Uri(GithubApiUrl)` with `BaseAddress = ResolveBaseUri("HTTP_GITHUB_BASE_URL", GithubApiUrl)`.

- [ ] **Step 4: Run tests, run slopwatch**

Run: `dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~HttpClientFactoryTests"` → PASS (4/4)
Run: `dotnet build --configuration Release` → 0 warnings
Run: `slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"` → no new issues

- [ ] **Step 5: Commit**

```bash
git add src/BadgeSmith.Api/Core/Http/HttpClientFactory.cs tests/BadgeSmith.Api.Tests/Http/HttpClientFactoryTests.cs
git commit -m "feat(http): allow env override of NuGet/GitHub base URLs for contract testing"
```

---

### Task 4: Shared test infrastructure, category taxonomy, stack fixture, health AOT contract

**Files:**
- Modify: `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj`
- Modify: existing unit test classes under `tests/BadgeSmith.Api.Tests/Routing/**`
- Create: `tests/BadgeSmith.Api.Tests/Testing/TestCategories.cs`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/LambdaRieClient.cs`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AwsTestSeeder.cs`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/BadgeSmithStackFixture.cs`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/ping.json`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/__files/.gitkeep`
- Create: `tests/BadgeSmith.Api.Tests/Functional/HealthContractTests.cs`
- Modify: `Directory.Packages.props` (via `dotnet add package` only)

**Interfaces:**
- Produces: category constants `TestCategories.Unit`, `Integration`, `Functional`, `AotContract`; collection `"contract"` (parallelization disabled); `BadgeSmithStackFixture` with `LambdaRieClient Lambda`, `IAmazonDynamoDB DynamoDb`, `IAmazonSecretsManager Secrets`, `const string HmacSecret = "contract-test-secret"`, `const string Org = "test-org"`; `LambdaRieClient.InvokeAsync(string method, string path, IReadOnlyDictionary<string,string>? headers = null, string? body = null)` → `LambdaHttpResponse(int StatusCode, Dictionary<string,string>? Headers, string? Body)`.
- Consumes: existing test project `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj`; image `BADGESMITH_TEST_IMAGE` (default `badge-smith:local`) from Task 1; env vars from Tasks 2–3.

- [ ] **Step 1: Add container/AWS packages to the existing test project through CPM**

Run these commands; do not hand-edit package versions:

```bash
dotnet add tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj package Testcontainers
dotnet add tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj package Testcontainers.LocalStack
dotnet add tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj package AWSSDK.DynamoDBv2
dotnet add tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj package AWSSDK.SecretsManager
```

Then add this item group to `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj` so WireMock mappings copy next to the test binaries:

```xml
  <ItemGroup>
    <None Include="Testing\Infrastructure\wiremock\**" CopyToOutputDirectory="PreserveNewest"/>
  </ItemGroup>
```

- [ ] **Step 2: Add the test category constants**

`tests/BadgeSmith.Api.Tests/Testing/TestCategories.cs`:

```csharp
namespace BadgeSmith.Api.Tests.Testing;

public static class TestCategories
{
    public const string Unit = "Unit";
    public const string Integration = "Integration";
    public const string Functional = "Functional";
    public const string AotContract = "AotContract";
}
```

- [ ] **Step 3: Label existing unit tests**

Add `using BadgeSmith.Api.Tests.Testing;` and class-level `[Trait("Category", TestCategories.Unit)]` to every existing test class:

```text
tests/BadgeSmith.Api.Tests/Routing/RouteValuesTests.cs
tests/BadgeSmith.Api.Tests/Routing/RouteResolverTests.cs
tests/BadgeSmith.Api.Tests/Routing/Patterns/TemplatePatternTests.cs
tests/BadgeSmith.Api.Tests/Routing/Patterns/RegexPatternTests.cs
tests/BadgeSmith.Api.Tests/Routing/Patterns/ExactPatternTests.cs
tests/BadgeSmith.Api.Tests/Routing/CorsHandler/ApplyResponseHeaders.cs
tests/BadgeSmith.Api.Tests/Routing/CorsHandler/CorsOptionsTests.cs
tests/BadgeSmith.Api.Tests/Routing/CorsHandler/HandlePreflightTests.cs
```

Example shape:

```csharp
using BadgeSmith.Api.Tests.Testing;

namespace BadgeSmith.Api.Tests.Routing;

[Trait("Category", TestCategories.Unit)]
public sealed class RouteValuesTests
{
    // existing tests unchanged
}
```

Verify the unit taxonomy immediately:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=Unit"
```

Expected: existing routing/CORS tests pass; no Docker dependency is started.

- [ ] **Step 4: Implement `LambdaRieClient`**

`tests/BadgeSmith.Api.Tests/Testing/Infrastructure/LambdaRieClient.cs`:

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

public sealed record LambdaHttpResponse(
    [property: JsonPropertyName("statusCode")] int StatusCode,
    [property: JsonPropertyName("headers")] Dictionary<string, string>? Headers,
    [property: JsonPropertyName("body")] string? Body);

public sealed class LambdaRieClient(Uri invocationBase)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);

    public async Task<LambdaHttpResponse> InvokeAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null,
        CancellationToken ct = default)
    {
        var evt = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["version"] = "2.0",
            ["routeKey"] = "$default",
            ["rawPath"] = path,
            ["headers"] = headers ?? new Dictionary<string, string>(StringComparer.Ordinal),
            ["requestContext"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["http"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["method"] = method, ["path"] = path },
                ["stage"] = "$default",
                ["requestId"] = Guid.NewGuid().ToString(),
            },
            ["body"] = body,
            ["isBase64Encoded"] = false,
        };

        using var content = new StringContent(JsonSerializer.Serialize(evt, Opts), Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(new Uri(invocationBase, "/2015-03-31/functions/function/invocations"), content, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<LambdaHttpResponse>(json, Opts)
               ?? throw new InvalidOperationException($"Unparseable RIE response: {json}");
    }
}
```

- [ ] **Step 5: Implement the reusable AWS seeder**

`tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AwsTestSeeder.cs`:

```csharp
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

public static class AwsTestSeeder
{
    public static async Task CreateTablesAndSecretsAsync(IAmazonDynamoDB dynamo, IAmazonSecretsManager secrets)
    {
        await CreatePkSkTableAsync(dynamo, "badge-smith-hmac-nonce", withGsi: false);
        await CreatePkSkTableAsync(dynamo, "badge-smith-github-org-secrets", withGsi: false);
        await CreatePkSkTableAsync(dynamo, "badge-smith-test-result", withGsi: true);

        await secrets.CreateSecretAsync(new CreateSecretRequest
        {
            Name = "badgesmith/github/test-org/testdata",
            SecretString = BadgeSmithStackFixture.HmacSecret,
        });
        await secrets.CreateSecretAsync(new CreateSecretRequest
        {
            Name = "badgesmith/github/test-org/package",
            SecretString = "dummy-github-pat",
        });

        await PutSecretMappingAsync(dynamo, "testdata", "badgesmith/github/test-org/testdata");
        await PutSecretMappingAsync(dynamo, "package", "badgesmith/github/test-org/package");
    }

    private static async Task PutSecretMappingAsync(IAmazonDynamoDB dynamo, string tokenTypeLower, string secretName)
    {
        await dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = "badge-smith-github-org-secrets",
            Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new($"ORG#{BadgeSmithStackFixture.Org}"),
                ["SK"] = new($"CONST#GITHUB#{tokenTypeLower}"),
                ["SecretName"] = new(secretName),
            },
        });
    }

    private static async Task CreatePkSkTableAsync(IAmazonDynamoDB dynamo, string name, bool withGsi)
    {
        var request = new CreateTableRequest
        {
            TableName = name,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions =
            [
                new AttributeDefinition("PK", ScalarAttributeType.S),
                new AttributeDefinition("SK", ScalarAttributeType.S),
            ],
            KeySchema =
            [
                new KeySchemaElement("PK", KeyType.HASH),
                new KeySchemaElement("SK", KeyType.RANGE),
            ],
        };

        if (withGsi)
        {
            request.AttributeDefinitions.Add(new AttributeDefinition("GSI1PK", ScalarAttributeType.S));
            request.AttributeDefinitions.Add(new AttributeDefinition("GSI1SK", ScalarAttributeType.S));
            request.GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = "GSI1",
                    KeySchema =
                    [
                        new KeySchemaElement("GSI1PK", KeyType.HASH),
                        new KeySchemaElement("GSI1SK", KeyType.RANGE),
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                },
            ];
        }

        await dynamo.CreateTableAsync(request);
    }
}
```

- [ ] **Step 6: Implement the reusable Testcontainers stack fixture**

`tests/BadgeSmith.Api.Tests/Testing/Infrastructure/BadgeSmithStackFixture.cs`:

```csharp
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.SecretsManager;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.LocalStack;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

[CollectionDefinition("contract", DisableParallelization = true)]
public sealed class ContractCollection : ICollectionFixture<BadgeSmithStackFixture>;

public sealed class BadgeSmithStackFixture : IAsyncLifetime
{
    public const string HmacSecret = "contract-test-secret";
    public const string Org = "test-org";

    private INetwork _network = null!;
    private LocalStackContainer _localstack = null!;
    private IContainer _wiremock = null!;
    private IContainer _lambda = null!;

    public LambdaRieClient Lambda { get; private set; } = null!;
    public IAmazonDynamoDB DynamoDb { get; private set; } = null!;
    public IAmazonSecretsManager Secrets { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var image = Environment.GetEnvironmentVariable("BADGESMITH_TEST_IMAGE") ?? "badge-smith:local";
        var wiremockDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Testing", "Infrastructure", "wiremock"));

        _network = new NetworkBuilder().Build();

        _localstack = new LocalStackBuilder()
            .WithImage("localstack/localstack:4.6")
            .WithNetwork(_network)
            .WithNetworkAliases("localstack")
            .Build();

        _wiremock = new ContainerBuilder()
            .WithImage("wiremock/wiremock:3.9.1")
            .WithNetwork(_network)
            .WithNetworkAliases("wiremock")
            .WithBindMount(wiremockDir, "/home/wiremock", AccessMode.ReadOnly)
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/__admin/health")))
            .Build();

        await Task.WhenAll(_localstack.StartAsync(), _wiremock.StartAsync());

        var creds = new BasicAWSCredentials("test", "test");
        DynamoDb = new AmazonDynamoDBClient(creds, new AmazonDynamoDBConfig { ServiceURL = _localstack.GetConnectionString(), AuthenticationRegion = "eu-central-1" });
        Secrets = new AmazonSecretsManagerClient(creds, new AmazonSecretsManagerConfig { ServiceURL = _localstack.GetConnectionString(), AuthenticationRegion = "eu-central-1" });

        await AwsTestSeeder.CreateTablesAndSecretsAsync(DynamoDb, Secrets);

        _lambda = new ContainerBuilder()
            .WithImage(image)
            .WithNetwork(_network)
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithEnvironment(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOTNET_ENVIRONMENT"] = "Production",
                ["AWS_ACCESS_KEY_ID"] = "test",
                ["AWS_SECRET_ACCESS_KEY"] = "test",
                ["AWS_REGION"] = "eu-central-1",
                ["AWS_ENDPOINT_URL_DYNAMODB"] = "http://localstack:4566",
                ["AWS_ENDPOINT_URL_SECRETS_MANAGER"] = "http://localstack:4566",
                ["AWS_RESOURCE_TEST_RESULTS_TABLE"] = "badge-smith-test-result",
                ["AWS_RESOURCE_NONCE_TABLE"] = "badge-smith-hmac-nonce",
                ["AWS_RESOURCE_ORG_SECRETS_TABLE"] = "badge-smith-github-org-secrets",
                ["HTTP_NUGET_BASE_URL"] = "http://wiremock:8080/nuget/",
                ["HTTP_GITHUB_BASE_URL"] = "http://wiremock:8080/github/",
            })
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
            .Build();

        try
        {
            await _lambda.StartAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start image '{image}'. Build it first: docker build --target lambda-image -t badge-smith:local .", ex);
        }

        Lambda = new LambdaRieClient(new Uri($"http://{_lambda.Hostname}:{_lambda.GetMappedPublicPort(8080)}"));
    }

    public async ValueTask DisposeAsync()
    {
        await _lambda.DisposeAsync();
        await _wiremock.DisposeAsync();
        await _localstack.DisposeAsync();
        await _network.DisposeAsync();
        DynamoDb.Dispose();
        Secrets.Dispose();
    }
}
```

- [ ] **Step 7: Create placeholder WireMock content**

```bash
mkdir -p tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/__files
printf '{"request":{"method":"GET","urlPath":"/__ping"},"response":{"status":200}}' > tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/ping.json
touch tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/__files/.gitkeep
```

- [ ] **Step 8: Write the first functional AOT-contract test**

`tests/BadgeSmith.Api.Tests/Functional/HealthContractTests.cs`:

```csharp
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
[Trait("Category", TestCategories.AotContract)]
public sealed class HealthContractTests(BadgeSmithStackFixture stack)
{
    [Fact]
    public async Task Health_Should_Return200_WithNoCacheHeaders()
    {
        var response = await stack.Lambda.InvokeAsync("GET", "/health");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Healthy", response.Body, StringComparison.Ordinal);
        Assert.NotNull(response.Headers);
        Assert.Contains("no-store", response.Headers["Cache-Control"], StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 9: Run filters, build, slopwatch, commit**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=Unit"
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract&FullyQualifiedName~HealthContractTests"
dotnet build --configuration Release
slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"
```

Expected: unit tests pass without Docker; health AOT-contract test passes against the prebuilt `badge-smith:local` image; build has 0 warnings; Slopwatch reports no new issues.

```bash
git add tests/BadgeSmith.Api.Tests Directory.Packages.props
git commit -m "test(infra): add shared Testcontainers AOT contract infrastructure"
```

---

### Task 5: HMAC signer, ingestion round-trip + auth failures + tests-badge/redirect

**Files:**
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/HmacTestSigner.cs`
- Create: `tests/BadgeSmith.Api.Tests/Functional/TestResultsContractTests.cs`

**Interfaces:**
- Consumes: `BadgeSmithStackFixture`, `AwsTestSeeder`, `LambdaRieClient`, and category constants from Task 4.
- Produces: `HmacTestSigner.Sign(string body, string secret, DateTimeOffset? timestamp = null, string? nonce = null)` → `(string Signature, string Timestamp, string Nonce)`.

- [ ] **Step 1: Implement `HmacTestSigner`**

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

public static class HmacTestSigner
{
    public static (string Signature, string Timestamp, string Nonce) Sign(
        string body, string secret, DateTimeOffset? timestamp = null, string? nonce = null)
    {
        var ts = (timestamp ?? DateTimeOffset.UtcNow).UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return ($"sha256={Convert.ToHexString(hash).ToLowerInvariant()}", ts, nonce ?? Guid.NewGuid().ToString("N"));
    }
}
```

- [ ] **Step 2: Write the failing test class (write all tests, then run)**

`tests/BadgeSmith.Api.Tests/Functional/TestResultsContractTests.cs` — all route values lowercase (pins current GSI case behavior):

```csharp
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
[Trait("Category", TestCategories.AotContract)]
public sealed class TestResultsContractTests(BadgeSmithStackFixture stack)
{
    private const string IngestPath = "/tests/results/linux/test-org/test-repo/main";
    private const string BadgePath = "/badges/tests/linux/test-org/test-repo/main";

    private static string Payload(string runId, string ts) => $$"""
        {"platform":"linux","passed":10,"failed":0,"skipped":1,"total":11,
         "url_html":"https://github.com/test-org/test-repo/runs/1",
         "timestamp":"{{ts}}","commit":"abc1234","run_id":"{{runId}}",
         "workflow_run_url":"https://github.com/test-org/test-repo/actions/runs/1"}
        """;

    private static Dictionary<string, string> AuthHeaders(string body)
    {
        var (sig, ts, nonce) = HmacTestSigner.Sign(body, BadgeSmithStackFixture.HmacSecret);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x-signature"] = sig, ["x-timestamp"] = ts, ["x-nonce"] = nonce,
            ["content-type"] = "application/json",
        };
    }

    [Fact]
    public async Task Ingestion_Then_Badge_RoundTrip()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, AuthHeaders(body), body);
        Assert.Equal(201, post.StatusCode);

        var badge = await stack.Lambda.InvokeAsync("GET", BadgePath);
        Assert.Equal(200, badge.StatusCode);
        Assert.Contains("\"schemaVersion\":1", badge.Body, StringComparison.Ordinal);
        Assert.Contains("passed", badge.Body, StringComparison.Ordinal);
        Assert.NotNull(badge.Headers);
        Assert.StartsWith("\"", badge.Headers["ETag"], StringComparison.Ordinal);

        var cached = await stack.Lambda.InvokeAsync("GET", BadgePath,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["if-none-match"] = badge.Headers["ETag"] });
        Assert.Equal(304, cached.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Reject_BadSignature_With401()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var headers = AuthHeaders(body);
        headers["x-signature"] = "sha256=" + new string('0', 64);
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, headers, body);
        Assert.Equal(401, post.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Reject_StaleTimestamp_With400()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var (sig, ts, nonce) = HmacTestSigner.Sign(body, BadgeSmithStackFixture.HmacSecret,
            timestamp: DateTimeOffset.UtcNow.AddMinutes(-10));
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x-signature"] = sig, ["x-timestamp"] = ts, ["x-nonce"] = nonce,
        };
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, headers, body);
        Assert.Equal(400, post.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Reject_NonceReplay_With400()
    {
        var nonce = Guid.NewGuid().ToString("N");
        var body1 = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var (sig1, ts1, _) = HmacTestSigner.Sign(body1, BadgeSmithStackFixture.HmacSecret, nonce: nonce);
        var first = await stack.Lambda.InvokeAsync("POST", IngestPath,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["x-signature"] = sig1, ["x-timestamp"] = ts1, ["x-nonce"] = nonce }, body1);
        Assert.Equal(201, first.StatusCode);

        var body2 = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var (sig2, ts2, _) = HmacTestSigner.Sign(body2, BadgeSmithStackFixture.HmacSecret, nonce: nonce);
        var replay = await stack.Lambda.InvokeAsync("POST", IngestPath,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["x-signature"] = sig2, ["x-timestamp"] = ts2, ["x-nonce"] = nonce }, body2);
        Assert.Equal(400, replay.StatusCode);
    }

    [Fact]
    public async Task Ingestion_MalformedHexSignature_PinsCurrentBehavior_500()
    {
        // Known bug (findings doc §2): malformed hex throws FormatException → 500.
        // Wave 1 will change this to 401 and update this assertion.
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var headers = AuthHeaders(body);
        headers["x-signature"] = "sha256=zzzz";
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, headers, body);
        Assert.Equal(500, post.StatusCode);
    }

    [Fact]
    public async Task Ingestion_MissingAuthHeaders_Should_Return400()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, body: body);
        Assert.Equal(400, post.StatusCode);
    }

    [Fact]
    public async Task Badge_UnknownRepo_Should_Return404()
    {
        var badge = await stack.Lambda.InvokeAsync("GET", "/badges/tests/linux/test-org/no-such-repo/main");
        Assert.Equal(404, badge.StatusCode);
    }

    [Fact]
    public async Task Redirect_Should_Return302_WithLocation()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, AuthHeaders(body), body);
        Assert.Equal(201, post.StatusCode);

        var redirect = await stack.Lambda.InvokeAsync("GET", "/redirect/test-results/linux/test-org/test-repo/main");
        Assert.Equal(302, redirect.StatusCode);
        Assert.NotNull(redirect.Headers);
        Assert.Contains("github.com", redirect.Headers["Location"], StringComparison.Ordinal);
    }
}
```

- [ ] **Step 3: Run, iterate until green**

Run: `dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract&FullyQualifiedName~TestResultsContractTests"`
Expected: all tests PASS. If a 500 appears where 201 is expected, read the lambda container logs (`docker logs <container>` — Testcontainers prints the id) before touching assertions; likely causes are seeding order or header casing.

- [ ] **Step 4: slopwatch + commit**

```bash
slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"
git add tests/BadgeSmith.Api.Tests
git commit -m "test(contract): cover HMAC ingestion round-trip, auth failures, tests badge, redirect"
```

---

### Task 6: WireMock mappings + NuGet/GitHub badge contract tests

**Files:**
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/nuget-index-ok.json`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/nuget-index-404.json`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/github-versions-ok.json`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/github-versions-401.json`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/__files/nuget-contracttest-index.json`
- Create: `tests/BadgeSmith.Api.Tests/Functional/PackageBadgeContractTests.cs`

**Interfaces:**
- Consumes: WireMock mount + `HTTP_*_BASE_URL` prefixes (`/nuget/`, `/github/`) from Task 4.

- [ ] **Step 1: Record the NuGet body from the real API (spec: recorded, not invented)**

```bash
curl -s https://api.nuget.org/v3-flatcontainer/newtonsoft.json/index.json \
  | python -c "import json,sys; d=json.load(sys.stdin); d['versions']=d['versions'][:3]+['13.0.4-beta1','13.0.3']; print(json.dumps(d))" \
  > tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/__files/nuget-contracttest-index.json
```

(Real response shape, truncated + one prerelease appended so both stable and prerelease paths are exercised. Highest stable in the file must be `13.0.3` — verify by eye.)

- [ ] **Step 2: Write the mappings**

`nuget-index-ok.json`:

```json
{
  "request": { "method": "GET", "urlPath": "/nuget/v3-flatcontainer/contracttest.pkg/index.json" },
  "response": {
    "status": 200,
    "headers": { "Content-Type": "application/json", "ETag": "\"contract-etag-1\"" },
    "bodyFileName": "nuget-contracttest-index.json"
  }
}
```

`nuget-index-404.json`:

```json
{
  "request": { "method": "GET", "urlPathPattern": "/nuget/v3-flatcontainer/missing.pkg/index.json" },
  "response": { "status": 404 }
}
```

`github-versions-ok.json` (body shape from the GitHub REST docs for org package versions — `name` is the version string, which is the only field the API reads):

```json
{
  "request": { "method": "GET", "urlPath": "/github/orgs/test-org/packages/nuget/contracttest.pkg/versions" },
  "response": {
    "status": 200,
    "headers": { "Content-Type": "application/json" },
    "jsonBody": [ { "id": 1, "name": "2.1.0" }, { "id": 2, "name": "2.2.0-preview.1" }, { "id": 3, "name": "1.9.9" } ]
  }
}
```

`github-versions-401.json`:

```json
{
  "request": { "method": "GET", "urlPath": "/github/orgs/unauthorized-org/packages/nuget/any.pkg/versions" },
  "response": { "status": 401, "jsonBody": { "message": "Bad credentials" } }
}
```

- [ ] **Step 3: Write the failing tests**

`tests/BadgeSmith.Api.Tests/Functional/PackageBadgeContractTests.cs`:

```csharp
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
[Trait("Category", TestCategories.AotContract)]
public sealed class PackageBadgeContractTests(BadgeSmithStackFixture stack)
{
    [Fact]
    public async Task NuGetBadge_Should_ReturnHighestStable()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg");
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"13.0.3\"", r.Body, StringComparison.Ordinal);
        Assert.Contains("\"color\":\"blue\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NuGetBadge_WithPrerelease_Should_ReturnPrereleaseVersion()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?prerelease=true");
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("13.0.4-beta1", r.Body, StringComparison.Ordinal);
        Assert.Contains("\"color\":\"orange\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NuGetBadge_UnknownPackage_Should_Return404()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/missing.pkg");
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task NuGetBadge_InvalidVersionRange_Should_Return400()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?version=not-a-range");
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task NuGetBadge_Should_Honor_IfNoneMatch()
    {
        var first = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg");
        Assert.Equal(200, first.StatusCode);
        var second = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["if-none-match"] = first.Headers!["ETag"] });
        Assert.Equal(304, second.StatusCode);
    }

    [Fact]
    public async Task GitHubBadge_Should_ReturnHighestStable()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg");
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"2.1.0\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHubBadge_OrgWithoutSecret_Should_Return401()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/github/unknown-org/some.pkg");
        Assert.Equal(401, r.StatusCode);
    }

    [Fact]
    public async Task PackagesRoute_UnknownProvider_Should_Return400()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/npm/some.pkg");
        Assert.Equal(400, r.StatusCode);
    }
}
```

- [ ] **Step 4: Run, iterate until green**

Run: `dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract&FullyQualifiedName~PackageBadgeContractTests"`
Expected: all PASS. If NuGet tests 500: check WireMock admin (`curl http://localhost:<mapped>/__admin/requests`) for near-misses — path prefix must match `HTTP_NUGET_BASE_URL` exactly.

- [ ] **Step 5: slopwatch + commit**

```bash
slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"
git add tests/BadgeSmith.Api.Tests
git commit -m "test(contract): NuGet/GitHub badge contracts against recorded WireMock stubs"
```

---

### Task 7: Routing/CORS contracts + "test the tester" drill

**Files:**
- Create: `tests/BadgeSmith.Api.Tests/Functional/RoutingContractTests.cs`

- [ ] **Step 1: Write the tests**

```csharp
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
[Trait("Category", TestCategories.AotContract)]
public sealed class RoutingContractTests(BadgeSmithStackFixture stack)
{
    [Fact]
    public async Task UnknownRoute_Should_Return404()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/nope/nothing/here");
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Head_Should_BeRoutedLikeGet()
    {
        var r = await stack.Lambda.InvokeAsync("HEAD", "/health");
        Assert.Equal(200, r.StatusCode);
    }

    [Fact]
    public async Task OptionsPreflight_Should_ReturnCorsHeaders()
    {
        var r = await stack.Lambda.InvokeAsync("OPTIONS", "/badges/packages/nuget/contracttest.pkg",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["origin"] = "https://example.com",
                ["access-control-request-method"] = "GET",
            });
        Assert.Equal(204, r.StatusCode);
        Assert.NotNull(r.Headers);
        Assert.Equal("*", r.Headers["Access-Control-Allow-Origin"]);
        Assert.Contains("GET", r.Headers["Access-Control-Allow-Methods"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Responses_Should_CarryCorsHeader()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/health");
        Assert.NotNull(r.Headers);
        Assert.Equal("*", r.Headers["Access-Control-Allow-Origin"]);
    }
}
```

- [ ] **Step 2: Run to green**

Run: `dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract"`
Expected: full suite PASS.

- [ ] **Step 3: Test-the-tester drill (spec acceptance criterion; no commit of the breakage)**

```bash
git checkout -b scratch/aot-net-proof
# Remove the ShieldsBadgeResponse registration line from
# src/BadgeSmith.Api/Core/Infrastructure/LambdaFunctionJsonSerializerContext.cs:
#   [JsonSerializable(typeof(ShieldsBadgeResponse))]
docker build --target lambda-image -t badge-smith:local .
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract"
```

Expected: badge tests FAIL (500s from the AOT binary — serializer cannot serialize the type). This proves the net catches AOT-only failures.

```bash
git checkout master && git branch -D scratch/aot-net-proof
docker build --target lambda-image -t badge-smith:local .   # restore good image
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract"   # green again
```

- [ ] **Step 4: slopwatch + commit**

```bash
slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"
git add tests/BadgeSmith.Api.Tests
git commit -m "test(contract): routing, HEAD, and CORS preflight contracts"
```

Record the drill outcome in the Task 13 README.

---

### Task 8: k6 script — env config + RIE mode

**Files:**
- Modify: `scripts/k6-perf-test.js`

**Interfaces:**
- Produces: env contract `K6_API_URL` (default: current hardcoded URL), `K6_DURATION`, `K6_VUS`, `K6_TARGET_MODE=http|rie`. In `rie` mode every request wraps into an APIGW v2 event POSTed to `${K6_API_URL}/2015-03-31/functions/function/invocations`, and the check reads `statusCode` from the invocation response JSON.

- [ ] **Step 1: Add env reading at the top of the script**

Replace the `const BASE_URL = "https://g4yecfi5hl...";` line with:

```javascript
const BASE_URL = __ENV.K6_API_URL || "https://g4yecfi5hl.execute-api.eu-central-1.amazonaws.com";
const TARGET_MODE = __ENV.K6_TARGET_MODE || "http"; // http | rie
const DURATION = __ENV.K6_DURATION || null;
const VUS = __ENV.K6_VUS ? parseInt(__ENV.K6_VUS, 10) : null;
```

If the script's `options` define stages/vus/duration, apply `DURATION`/`VUS` overrides where they are defined (keep existing values as defaults).

- [ ] **Step 2: Add the request wrapper and route ALL existing http.get calls through it**

```javascript
function invoke(method, path, headers) {
  if (TARGET_MODE === "http") {
    return http.request(method, `${BASE_URL}${path}`, null, { headers });
  }
  const event = JSON.stringify({
    version: "2.0", routeKey: "$default", rawPath: path,
    headers: headers || {},
    requestContext: { http: { method, path }, stage: "$default", requestId: `k6-${__VU}-${__ITER}` },
    isBase64Encoded: false,
  });
  const res = http.post(`${BASE_URL}/2015-03-31/functions/function/invocations`, event,
    { headers: { "Content-Type": "application/json" } });
  // Surface the lambda's status code for checks:
  try { res.lambdaStatus = JSON.parse(res.body).statusCode; } catch (e) { res.lambdaStatus = 0; }
  return res;
}
```

Update the existing checks so status assertions use `res.lambdaStatus || res.status` (works in both modes).

- [ ] **Step 3: Verify both modes**

Run (http mode, against prod — read-only GETs): `k6 run --vus 1 --duration 5s scripts/k6-perf-test.js -e K6_API_URL=https://api.localstackfor.net`
Expected: completes; checks pass.
Run (rie mode, with the Task 1 container running on :9000): `k6 run --vus 1 --duration 5s scripts/k6-perf-test.js -e K6_API_URL=http://localhost:9000 -e K6_TARGET_MODE=rie`
Expected: completes; health checks pass (badge routes may 404/500 without seeded stack — acceptable for this verification; the harness runs the full stack).

- [ ] **Step 4: Commit**

```bash
git add scripts/k6-perf-test.js
git commit -m "feat(scripts): k6 env configuration and RIE invocation mode"
```

---

### Task 9: mstat export stage + `perf-baseline.sh` (+ thin ps1 wrapper)

**Files:**
- Modify: `src/BadgeSmith.Api/Dockerfile`
- Create: `scripts/perf-baseline.sh`
- Create: `scripts/perf-baseline.ps1`
- Create: `docs/research/baselines/` (first JSON arrives in Task 10)

**Interfaces:**
- Produces: `perf-baseline.sh --label <name> [--upstream mock|real] [--arch amd64|arm64]` → writes `docs/research/baselines/<UTC-date>-<label>.json` (schema per spec).

- [ ] **Step 1: Dockerfile — optional mstat + export stage**

In the build stage, add `ARG MSTAT=false` next to the other ARGs, extend the publish command with `-p:IlcGenerateMstatFile=${MSTAT}`, and after publish add:

```dockerfile
RUN if [ "$MSTAT" = "true" ]; then \
      find /src -name '*.mstat' -exec cp {} ${PUBLISH_DIR}/bootstrap.mstat \; ; \
    fi
```

At the end of the file add:

```dockerfile
###############################
# Stage 4: Export mstat (build with --build-arg MSTAT=true)
###############################
FROM scratch AS export-mstat
COPY --from=build /artifacts/publish/bootstrap.mstat /bootstrap.mstat
```

Verify: `docker build --target lambda-image -t badge-smith:local .` still succeeds unchanged (MSTAT defaults false).

- [ ] **Step 2: Write `scripts/perf-baseline.sh`**

```bash
#!/usr/bin/env bash
set -euo pipefail

LABEL="baseline" ; UPSTREAM="mock" ; ARCH="amd64" ; VUS="${K6_VUS:-5}" ; DURATION="${K6_DURATION:-60s}"
while [[ $# -gt 0 ]]; do case "$1" in
  --label) LABEL="$2"; shift 2;; --upstream) UPSTREAM="$2"; shift 2;; --arch) ARCH="$2"; shift 2;;
  *) echo "unknown arg $1"; exit 1;; esac; done

RID="linux-x64"; PLATFORM="linux/amd64"
[[ "$ARCH" == "arm64" ]] && RID="linux-arm64" && PLATFORM="linux/arm64"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$ROOT/docs/research/baselines"; mkdir -p "$OUT_DIR"
STAMP="$(date -u +%Y-%m-%d)"; OUT="$OUT_DIR/$STAMP-$LABEL.json"
IMG="badge-smith:perf-$ARCH"

echo "== build image + artifacts =="
docker build --platform "$PLATFORM" --build-arg RID="$RID" --target lambda-image -t "$IMG" "$ROOT"
docker build --platform "$PLATFORM" --build-arg RID="$RID" --build-arg MSTAT=true --target export-mstat -o "$ROOT/artifacts/mstat" "$ROOT"
docker build --platform "$PLATFORM" --build-arg RID="$RID" --target export-zip -o "$ROOT/artifacts" "$ROOT"
ZIP="$ROOT/artifacts/badge-lambda-$RID.zip"
ZIP_BYTES=$(stat -c%s "$ZIP" 2>/dev/null || stat -f%z "$ZIP")
BIN_BYTES=$(unzip -l "$ZIP" | awk '/bootstrap$/ {print $1}')

echo "== boot stack =="
NET="bs-perf-net"; docker network rm "$NET" 2>/dev/null || true; docker network create "$NET"
docker run -d --name bs-perf-ls --network "$NET" --network-alias localstack localstack/localstack:4.6
docker run -d --name bs-perf-wm --network "$NET" --network-alias wiremock \
  -v "$ROOT/tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock:/home/wiremock:ro" wiremock/wiremock:3.9.1
sleep 12
bash "$ROOT/scripts/perf-baseline-seed.sh" "$NET"   # created in Step 3

NUGET_URL="http://wiremock:8080/nuget/"; GITHUB_URL="http://wiremock:8080/github/"
if [[ "$UPSTREAM" == "real" ]]; then NUGET_URL="https://api.nuget.org/"; GITHUB_URL="https://api.github.com/"; fi

START_NS=$(date +%s%N)
docker run -d --name bs-perf-lambda --network "$NET" -p 9000:8080 \
  -e DOTNET_ENVIRONMENT=Production \
  -e AWS_ACCESS_KEY_ID=test -e AWS_SECRET_ACCESS_KEY=test -e AWS_REGION=eu-central-1 \
  -e AWS_ENDPOINT_URL_DYNAMODB=http://localstack:4566 \
  -e AWS_ENDPOINT_URL_SECRETS_MANAGER=http://localstack:4566 \
  -e AWS_RESOURCE_TEST_RESULTS_TABLE=badge-smith-test-result \
  -e AWS_RESOURCE_NONCE_TABLE=badge-smith-hmac-nonce \
  -e AWS_RESOURCE_ORG_SECRETS_TABLE=badge-smith-github-org-secrets \
  -e HTTP_NUGET_BASE_URL="$NUGET_URL" -e HTTP_GITHUB_BASE_URL="$GITHUB_URL" \
  "$IMG"
until curl -s -o /dev/null -w '%{http_code}' -XPOST http://localhost:9000/2015-03-31/functions/function/invocations \
  -d '{"version":"2.0","routeKey":"$default","rawPath":"/health","headers":{},"requestContext":{"http":{"method":"GET","path":"/health"},"stage":"$default","requestId":"warm"},"isBase64Encoded":false}' | grep -q 200; do sleep 0.2; done
READY_MS=$(( ($(date +%s%N) - START_NS) / 1000000 ))
RSS_IDLE=$(docker stats --no-stream --format '{{.MemUsage}}' bs-perf-lambda | awk '{print $1}')

echo "== k6 =="
K6_JSON="$ROOT/artifacts/k6-summary.json"
k6 run --summary-export "$K6_JSON" -e K6_API_URL=http://localhost:9000 -e K6_TARGET_MODE=rie \
  -e K6_VUS="$VUS" -e K6_DURATION="$DURATION" "$ROOT/scripts/k6-perf-test.js" &
K6_PID=$!
RSS_PEAK_KB=0
while kill -0 $K6_PID 2>/dev/null; do
  CUR=$(docker stats --no-stream --format '{{.MemUsage}}' bs-perf-lambda | awk '{print $1}')
  CUR_KB=$(numfmt --from=iec "${CUR%B}" 2>/dev/null || echo 0); CUR_KB=$((CUR_KB / 1024))
  (( CUR_KB > RSS_PEAK_KB )) && RSS_PEAK_KB=$CUR_KB
  sleep 1
done
wait $K6_PID

python - "$OUT" "$K6_JSON" <<EOF
import json, subprocess, sys
out, k6file = sys.argv[1], sys.argv[2]
k6 = json.load(open(k6file))
m = k6["metrics"]["http_req_duration"]
json.dump({
  "date": "$STAMP", "label": "$LABEL",
  "gitSha": subprocess.check_output(["git","rev-parse","--short","HEAD"]).decode().strip(),
  "arch": "$ARCH", "upstream": "$UPSTREAM",
  "image": {"binaryBytes": int("$BIN_BYTES"), "zipBytes": int("$ZIP_BYTES"), "mstat": "artifacts/mstat/bootstrap.mstat"},
  "boot": {"startToReadyMs": int("$READY_MS")},
  "k6": {"p50Ms": m.get("med"), "p95Ms": m.get("p(95)"), "p99Ms": m.get("p(99)"),
          "rps": k6["metrics"]["http_reqs"].get("rate"), "errorRate": k6["metrics"].get("http_req_failed",{}).get("value",0)},
  "memory": {"rssIdle": "$RSS_IDLE", "rssPeakKb": int("$RSS_PEAK_KB")},
}, open(out,"w"), indent=2)
print("wrote", out)
EOF

docker rm -f bs-perf-lambda bs-perf-wm bs-perf-ls; docker network rm "$NET"
```

- [ ] **Step 3: Write `scripts/perf-baseline-seed.sh` (tables + secrets via awscli against LocalStack)**

```bash
#!/usr/bin/env bash
set -euo pipefail
export AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=eu-central-1
EP="--endpoint-url http://localhost:4566"
docker port bs-perf-ls 4566 >/dev/null 2>&1 && EP="--endpoint-url http://localhost:$(docker port bs-perf-ls 4566/tcp | head -1 | cut -d: -f2)"
aws $EP dynamodb create-table --table-name badge-smith-hmac-nonce \
  --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S \
  --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE --billing-mode PAY_PER_REQUEST
aws $EP dynamodb create-table --table-name badge-smith-github-org-secrets \
  --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S \
  --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE --billing-mode PAY_PER_REQUEST
aws $EP dynamodb create-table --table-name badge-smith-test-result \
  --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S AttributeName=GSI1PK,AttributeType=S AttributeName=GSI1SK,AttributeType=S \
  --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes 'IndexName=GSI1,KeySchema=[{AttributeName=GSI1PK,KeyType=HASH},{AttributeName=GSI1SK,KeyType=RANGE}],Projection={ProjectionType=ALL}' \
  --billing-mode PAY_PER_REQUEST
aws $EP secretsmanager create-secret --name badgesmith/github/test-org/testdata --secret-string contract-test-secret
aws $EP secretsmanager create-secret --name badgesmith/github/test-org/package --secret-string dummy-github-pat
aws $EP dynamodb put-item --table-name badge-smith-github-org-secrets \
  --item '{"PK":{"S":"ORG#test-org"},"SK":{"S":"CONST#GITHUB#testdata"},"SecretName":{"S":"badgesmith/github/test-org/testdata"}}'
aws $EP dynamodb put-item --table-name badge-smith-github-org-secrets \
  --item '{"PK":{"S":"ORG#test-org"},"SK":{"S":"CONST#GITHUB#package"},"SecretName":{"S":"badgesmith/github/test-org/package"}}'
```

Note: LocalStack container publishes 4566 on a random host port here — the `docker port` line resolves it; the k6/lambda side talks to LocalStack over the Docker network alias, so only the seeder needs the host port.

- [ ] **Step 4: Thin ps1 wrapper `scripts/perf-baseline.ps1`**

```powershell
#!/usr/bin/env pwsh
# Thin wrapper — the single source of truth is perf-baseline.sh (requires Git Bash on Windows).
& bash "$PSScriptRoot/perf-baseline.sh" @args
exit $LASTEXITCODE
```

- [ ] **Step 5: Dry run + commit**

Run: `bash scripts/perf-baseline.sh --label smoke --upstream mock`
Expected: JSON file written under `docs/research/baselines/`, all containers cleaned up. Delete the smoke JSON (`rm docs/research/baselines/*-smoke.json`) — the real baseline is Task 10.

```bash
slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"
git add src/BadgeSmith.Api/Dockerfile scripts/perf-baseline.sh scripts/perf-baseline-seed.sh scripts/perf-baseline.ps1
git commit -m "feat(scripts): perf/memory baseline harness with mstat export"
```

---

### Task 10: Record and commit the reference baseline

**Files:**
- Create: `docs/research/baselines/<date>-pre-iteration.json` (mock) and `<date>-pre-iteration-real.json` (real)

- [ ] **Step 1: Run both modes**

```bash
bash scripts/perf-baseline.sh --label pre-iteration --upstream mock
bash scripts/perf-baseline.sh --label pre-iteration-real --upstream real
```

Expected: two JSON files with plausible numbers (binaryBytes ≈ 14M, zipBytes ≈ 6.3M for amd64; boot under a few seconds).

- [ ] **Step 2: Commit**

```bash
git add docs/research/baselines/
git commit -m "docs(baselines): record pre-iteration perf/memory reference (mock + real upstream)"
```

---

### Task 11: QEMU-free arm64 cross-compile + timeboxed local arm64 attempt

**Files:**
- Modify: `src/BadgeSmith.Api/Dockerfile`
- Modify: `scripts/build-lambda.sh` and `scripts/build-lambda.ps1` (default RID)

**Interfaces:**
- Produces: `docker build --platform linux/arm64 --target export-zip -o artifacts .` yields `badge-lambda-linux-arm64.zip` with an aarch64 `bootstrap`, built natively on an x64 host. Build scripts default `RID=linux-arm64` (release artifact parity with CDK).

- [ ] **Step 1: Rework the build stage for cross-compilation**

Replace the current `FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build` header block with:

```dockerfile
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG TARGETARCH
ARG PROJECT=src/BadgeSmith.Api/BadgeSmith.Api.csproj
ARG CONFIG=Release
ARG RID=
ARG MSTAT=false
ARG PUBLISH_DIR=/artifacts/publish

# Resolve RID from TARGETARCH unless explicitly overridden
RUN test -n "$RID" || true
ENV _RID=${RID}

RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev zip \
 && if [ "${TARGETARCH}" = "arm64" ] && [ "$(uname -m)" != "aarch64" ]; then \
      dpkg --add-architecture arm64 && apt-get update && \
      apt-get install -y --no-install-recommends gcc-aarch64-linux-gnu binutils-aarch64-linux-gnu zlib1g-dev:arm64; \
    fi \
 && rm -rf /var/lib/apt/lists/*
```

And change the publish RUN to compute the RID + pass cross props:

```dockerfile
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    RID="${_RID:-$([ "${TARGETARCH}" = "arm64" ] && echo linux-arm64 || echo linux-x64)}" && \
    EXTRA="" && \
    if [ "$RID" = "linux-arm64" ] && [ "$(uname -m)" != "aarch64" ]; then \
      EXTRA="-p:ObjCopyName=aarch64-linux-gnu-objcopy"; \
    fi && \
    dotnet publish ${PROJECT} -c ${CONFIG} -r ${RID} --self-contained true \
      -p:PublishAot=true -p:StripSymbols=true -p:DebugType=none -p:EnableTelemetry=false -p:EnableLocalStack=false \
      -p:IlcGenerateMstatFile=${MSTAT} ${EXTRA} \
      -p:EnableSourceControlManagerQueries=false -p:EmbedUntrackedSources=false \
      -o ${PUBLISH_DIR} \
 && chmod +x ${PUBLISH_DIR}/bootstrap
```

Downstream stages (`lambda-zip`, `export-zip`) derive `ZIP_NAME` from the same RID logic — pass `RID` through as before (callers set `--build-arg RID=` explicitly or rely on `--platform`).

- [ ] **Step 2: Verify cross build on the x64 host, no QEMU**

```bash
docker build --platform linux/arm64 --target export-zip -o artifacts .
unzip -o artifacts/badge-lambda-linux-arm64.zip -d /tmp/bs-arm
file /tmp/bs-arm/bootstrap
```

Expected: `ELF 64-bit LSB pie executable, ARM aarch64` and the build log shows the publish running on the amd64 SDK (no `exec format error`, no qemu). If the ILC link fails on missing arm64 libs, add the missing `*:arm64` package to the apt line and retry (record what was needed).

- [ ] **Step 3: Default RID → arm64 in both build scripts**

`scripts/build-lambda.sh:5`: change `RID="linux-x64"` → `RID="linux-arm64"`.
`scripts/build-lambda.ps1:5`: change `$Rid = "linux-x64"` → `$Rid = "linux-arm64"` (match the actual parameter syntax in the file).
Verify: `./scripts/build-lambda.sh --target zip --verbose` produces `artifacts/badge-lambda-linux-arm64.zip` (matches CDK's `Code.FromAsset` expectation).

- [ ] **Step 4: Timeboxed local arm64 run attempt (max 30 min)**

```bash
docker run --privileged --rm tonistiigi/binfmt --install arm64
docker build --platform linux/arm64 --target lambda-image -t badge-smith:local-arm64 .
BADGESMITH_TEST_IMAGE=badge-smith:local-arm64 dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract"
```

Expected (either outcome is acceptable, document it): PASS → local arm64 works under refreshed QEMU; hang/crash → stop at the timebox, document ".NET under qemu-user unsupported, amd64 stands locally, arm64 gate in CI". No insistence — decided.

- [ ] **Step 5: slopwatch + commit**

```bash
slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"
git add src/BadgeSmith.Api/Dockerfile scripts/build-lambda.sh scripts/build-lambda.ps1
git commit -m "build(docker): QEMU-free arm64 cross-compilation; default release RID to arm64"
```

---

### Task 12: CI — contract suite as deploy gate + nightly (⛔ REQUIRES FRESH APPROVAL)

**Files:**
- Modify: `.github/workflows/deploy.yml`
- Create: `.github/workflows/nightly-contract.yml`

Before touching these files, STOP and get explicit approval from Deniz (AGENTS.md CI gate). Present this task's diff plan first.

- [ ] **Step 1: Add the gate job to `deploy.yml`**

Insert before the `deploy` job and make `deploy` depend on it:

```yaml
  contract-tests:
    runs-on: ubuntu-24.04-arm
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Build Lambda image (native arm64)
        run: docker build --target lambda-image -t badge-smith:local .
      - name: Run contract suite against the prod artifact
        run: dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract" --logger "console;verbosity=normal"
        env:
          BADGESMITH_TEST_IMAGE: badge-smith:local
```

And in the `deploy` job add:

```yaml
    needs: contract-tests
```

- [ ] **Step 2: Create `nightly-contract.yml`**

```yaml
name: Nightly Contract Suite

on:
  schedule:
    - cron: "0 3 * * *"
  workflow_dispatch:

jobs:
  contract-tests:
    runs-on: ubuntu-24.04-arm
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Build Lambda image (native arm64)
        run: docker build --target lambda-image -t badge-smith:local .
      - name: Run contract suite
        run: dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract"
        env:
          BADGESMITH_TEST_IMAGE: badge-smith:local
```

- [ ] **Step 3: Validate + commit + verify on GitHub**

Run: `gh workflow run nightly-contract.yml --ref master` after pushing (or wait for the schedule) and confirm green.

```bash
git add .github/workflows/deploy.yml .github/workflows/nightly-contract.yml
git commit -m "ci: contract suite as deploy gate and nightly run on arm64 runners"
```

---

### Task 13: Documentation — contract README, ROADMAP promotion

**Files:**
- Create: `tests/BadgeSmith.Api.Tests/README.md`
- Modify: `docs/ROADMAP.md`

- [ ] **Step 1: Write the README (this is the single home of the image/env contract per the spec)**

Content must include: how to build the image (`docker build --target lambda-image -t badge-smith:local .`), `BADGESMITH_TEST_IMAGE`, the category taxonomy (`Category=Unit`, `Category=Integration`, `Category=Functional`, `Category=AotContract`) with exact `dotnet test --filter` examples, the full env-var table used by fixture AND harness (`AWS_ENDPOINT_URL_DYNAMODB`, `AWS_ENDPOINT_URL_SECRETS_MANAGER`, `HTTP_NUGET_BASE_URL`, `HTTP_GITHUB_BASE_URL`, `AWS_RESOURCE_*`), the RIE invocation endpoint, how to run the suite and the harness, the mock/real upstream switch, spike findings from Tasks 1–2, the test-the-tester drill result from Task 7, and the arm64 attempt outcome from Task 11.

- [ ] **Step 2: Promote iteration 0 in `docs/ROADMAP.md`**

Move the testing-strategy Inbox entry into the Status & Plan Mapping table:

```markdown
| Iteration 0 — AOT contract tier, baseline harness, multi-arch build | done | [plans/2026-07-02-iteration0-aot-contract-tier-plan.md](plans/2026-07-02-iteration0-aot-contract-tier-plan.md) | Contract suite gates deploys; baselines recorded under research/baselines/ |
```

Remove the corresponding Inbox bullet.

- [ ] **Step 3: Commit**

```bash
git add tests/BadgeSmith.Api.Tests/README.md docs/ROADMAP.md
git commit -m "docs: contract-tier README and ROADMAP promotion for iteration 0"
```

---

## Self-Review Notes

- Spec coverage: RIE tier (T1, T4–7), env plumbing incl. spike + fallback stop-point (T2–3), coverage matrix incl. pinned-bug rule (T5–6), test-the-tester (T7), k6 fix (T8), harness + mstat + JSON schema + both upstream modes (T9–10), cross-compile + RID defaults + binfmt timebox (T11), CI gate + nightly with approval stop (T12), README contract + ROADMAP (T13). Ordering rule respected: baseline (T10) records after infra tasks, before any Wave 1+/perf work.
- Known risk acknowledged inline: exact Testcontainers API names (`assignRandomHostPort`, `UntilHttpRequestIsSucceeded`) and the k6 summary-export field names may need minor adaptation to the installed package versions — executors should fix signatures against IntelliSense, not change behavior.
- Type consistency: `BadgeSmithStackFixture.HmacSecret`/`Org` consumed by `AwsTestSeeder`, `HmacTestSigner`, and tests; `LambdaHttpResponse` shape used across all functional AOT-contract classes; `perf-baseline-seed.sh` matches fixture seeding exactly.
