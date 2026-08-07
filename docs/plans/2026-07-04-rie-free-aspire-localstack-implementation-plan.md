# RIE-Free Aspire Testing And LocalStack Benchmark Implementation Plan

Status: Completed/historical in `991769e`. Do not execute this checklist. Current local
development guidance lives in `README.md`, current CDK commands live in
`build/BadgeSmith.CDK/README.md`, and current workstream status lives in
`docs/ROADMAP.md`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the RIE-based contract and benchmark path with Aspire Testing for contract/integration coverage and LocalStack for local benchmark execution.

**Architecture:** Contract tests run the existing Aspire AppHost through `DistributedApplicationTestingBuilder` and call `APIGatewayEmulator` over normal HTTP. k6 benchmarks run against a LocalStack-hosted Lambda HTTP endpoint, with DynamoDB and Secrets Manager seeded before both mock-upstream and real-upstream runs. Native AOT artifact verification is decided by a LocalStack Lambda container-image spike; RIE is not reintroduced if that spike fails.

**Tech Stack:** .NET 10, xUnit v3 on VSTest, Aspire.Hosting.Testing, LocalStack.Aspire.Hosting, Testcontainers for test-owned WireMock only, AWS SDK v4, Docker, LocalStack Lambda v2, k6, Slopwatch.

## Global Constraints

- Follow `AGENTS.md`: do not commit, amend, push, create PRs, change CI, or run deploy/release commands without explicit approval.
- Before any commit, present a concise change summary and a proposed Conventional Commit message, then wait for approval.
- Keep production code behavior unchanged unless a task explicitly says to remove RIE-only dead code.
- Do not add RIE support, RIE invocation paths, or RIE benchmark modes.
- Keep `src/BadgeSmith.Host` free of test-only WireMock mappings unless a task explicitly proves a clean non-polluting resource boundary.
- Manage NuGet packages with `dotnet add` and `dotnet remove`; do not hand-edit package references for package management.
- Keep xUnit v3 and VSTest syntax. Do not introduce TUnit or Microsoft.Testing.Platform filters.
- Use `DateTimeOffset.UtcNow` or `DateTime.UtcNow` only; `DateTime.Now`, `DateTimeOffset.Now`, and `DateTimeOffset.DateTime` are banned.
- After code or script changes, run `slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"` when available.
- For .NET verification, use `dotnet build --configuration Release` and targeted `dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter ...` commands.
- CI workflow edits remain a separate approval gate and are not part of this plan.

---

## File Structure

The implementation should converge on this file layout:

| Path | Responsibility |
| --- | --- |
| `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AspireContractFixture.cs` | Starts `BadgeSmith.Host` with Aspire Testing and exposes contract HTTP client plus AWS clients for seeding |
| `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/ContractHttpClient.cs` | Sends normal HTTP requests to the Aspire or LocalStack endpoint and returns status, headers, and body |
| `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AwsTestSeeder.cs` | Seeds LocalStack DynamoDB and Secrets Manager with deterministic contract data |
| `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/HmacTestSigner.cs` | Produces HMAC headers for ingestion contract tests |
| `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/**` | Test-owned upstream mappings and response files |
| `tests/BadgeSmith.Api.Tests/Functional/*ContractTests.cs` | Aspire-backed HTTP contract tests; no `AotContract` trait |
| `scripts/k6-perf-test.js` | HTTP-only k6 scenario; no RIE mode or Lambda response-envelope projection |
| `scripts/perf-baseline.sh` | LocalStack benchmark orchestrator and baseline writer |
| `scripts/perf-baseline-seed.sh` | Shared LocalStack DynamoDB and Secrets Manager seeding for benchmarks |
| `docs/research/2026-07-04-localstack-lambda-image-spike.md` | Spike evidence for LocalStack Lambda image endpoint choice |
| `docs/research/baselines/*.json` | LocalStack benchmark baselines only |
| `docs/ROADMAP.md` | Updated only after the verified implementation state is true |

Remove these RIE-specific files or code paths once replacement coverage exists:

| Path | Action |
| --- | --- |
| `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/LambdaRieClient.cs` | Delete |
| `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/BadgeSmithStackFixture.cs` | Replace with `AspireContractFixture.cs` or keep only non-RIE WireMock helper code under a new name |
| `scripts/k6-perf-test.js` | Delete `K6_TARGET_MODE=rie`, API Gateway event wrapping, and response-envelope projection |
| `scripts/perf-baseline.sh` | Delete direct Lambda-container RIE startup and invocation health checks |
| `docs/research/baselines/2026-07-02-pre-iteration.json` | Remove or supersede because it was generated through the abandoned RIE path |

---

### Task 1: Add Aspire Testing Fixture And HTTP Contract Client

**Files:**
- Modify: `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/ContractHttpClient.cs`
- Create: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AspireContractFixture.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AwsTestSeeder.cs`

**Interfaces:**
- Consumes: AppHost project `src/BadgeSmith.Host/BadgeSmith.Host.csproj`, resource names `APIGatewayEmulator` and `localstack`, table names from `src/shared/Constants.cs`.
- Produces: `AspireContractFixture.Api.InvokeAsync(string method, string path, IReadOnlyDictionary<string,string>? headers = null, string? body = null, CancellationToken ct = default)` returning `ContractHttpResponse`.

- [ ] **Step 1: Add Aspire Testing and AppHost references with CLI commands**

Run:

```powershell
dotnet add "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" package Aspire.Hosting.Testing
dotnet add "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" reference "src/BadgeSmith.Host/BadgeSmith.Host.csproj"
```

Expected: `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj` gains a `PackageReference` for `Aspire.Hosting.Testing` and a `ProjectReference` to `BadgeSmith.Host`. `Directory.Packages.props` already contains `Aspire.Hosting.Testing`, so no new version should be added unless the CLI normalizes ordering.

- [ ] **Step 2: Create the HTTP contract client**

Create `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/ContractHttpClient.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

public sealed record ContractHttpResponse(
    int StatusCode,
    Dictionary<string, string> Headers,
    string? Body);

public sealed class ContractHttpClient(HttpClient http) : IDisposable
{
    public async Task<ContractHttpResponse> InvokeAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (body is not null)
        {
            var contentType = GetContentType(headers);
            request.Content = new StringContent(body, Encoding.UTF8, contentType);
        }

        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                if (string.Equals(key, "content-type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _ = request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddHeaders(responseHeaders, response.Headers);
        AddHeaders(responseHeaders, response.Content.Headers);

        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return new ContractHttpResponse((int)response.StatusCode, responseHeaders, responseBody.Length == 0 ? null : responseBody);
    }

    private static string GetContentType(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is not null && headers.TryGetValue("content-type", out var contentType) && !string.IsNullOrWhiteSpace(contentType))
        {
            return contentType;
        }

        return "application/json";
    }

    private static void AddHeaders(Dictionary<string, string> target, HttpHeaders source)
    {
        foreach (var header in source)
        {
            var value = string.Join(',', header.Value);
            target[header.Key] = value;
            target[header.Key.ToLowerInvariant()] = value;
        }
    }

    public void Dispose()
    {
        http.Dispose();
    }
}
```

- [ ] **Step 3: Replace table-name coupling in `AwsTestSeeder`**

Modify `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AwsTestSeeder.cs` so it no longer depends on `BadgeSmithStackFixture`. Add constants at the top of the class:

```csharp
public const string HmacSecret = "contract-test-secret";
public const string Org = "test-org";

private const string TestResultsTableName = "badge-smith-test-result";
private const string NonceTableName = "badge-smith-hmac-nonce";
private const string OrgSecretsTableName = "badge-smith-github-org-secrets";
```

Replace all literal table names and fixture references in that file:

```csharp
await CreatePkSkTableAsync(dynamo, NonceTableName, withGsi: false);
await CreatePkSkTableAsync(dynamo, OrgSecretsTableName, withGsi: false);
await CreatePkSkTableAsync(dynamo, TestResultsTableName, withGsi: true);

await CreateSecretAsync(secrets, "badgesmith/github/test-org/testdata", HmacSecret);
await CreateSecretAsync(secrets, "badgesmith/github/test-org/package", "dummy-github-pat");
await CreateSecretAsync(secrets, "badgesmith/github/unauthorized-org/package", "dummy-github-pat");

await PutSecretMappingAsync(dynamo, "testdata", "badgesmith/github/test-org/testdata");
await PutSecretMappingAsync(dynamo, "package", "badgesmith/github/test-org/package");
await PutSecretMappingAsync(dynamo, "unauthorized-org", "package", "badgesmith/github/unauthorized-org/package");
```

Inside `PutSecretMappingAsync`, set:

```csharp
TableName = OrgSecretsTableName,
```

Inside `PutSecretMappingAsync(IAmazonDynamoDB dynamo, string tokenTypeLower, string secretName)`, call:

```csharp
await PutSecretMappingAsync(dynamo, Org, tokenTypeLower, secretName);
```

- [ ] **Step 4: Create the Aspire fixture**

Create `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AspireContractFixture.cs`:

```csharp
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

[CollectionDefinition("contract", DisableParallelization = true)]
public sealed class ContractFixtureRegistration : ICollectionFixture<AspireContractFixture>;

public sealed class AspireContractFixture : IAsyncLifetime
{
    private const string Region = "us-east-1";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication? _app;

    public ContractHttpClient Api { get; private set; } = null!;
    public IAmazonDynamoDB DynamoDb { get; private set; } = null!;
    public IAmazonSecretsManager Secrets { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.BadgeSmith_Host>()
            .ConfigureAwait(false);

        _app = await builder.BuildAsync().ConfigureAwait(false);

        using var startupCts = new CancellationTokenSource(StartupTimeout);
        await _app.StartAsync(startupCts.Token).ConfigureAwait(false);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("APIGatewayEmulator", startupCts.Token)
            .ConfigureAwait(false);

        var apiEndpoint = _app.GetEndpoint("APIGatewayEmulator", "http");
        Api = new ContractHttpClient(CreateNoRedirectClient(apiEndpoint));

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

    public async ValueTask DisposeAsync()
    {
        Api?.Dispose();
        DynamoDb?.Dispose();
        Secrets?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static HttpClient CreateNoRedirectClient(Uri baseAddress)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(60),
        };
    }
}
```

- [ ] **Step 5: Run the first targeted build**

Run:

```powershell
dotnet build "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --configuration Release
```

Expected: if the generated AppHost type or endpoint overload differs, the build fails with a compile-time error. Fix only the generated type name or endpoint lookup call, then rerun until the test project builds with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 6: Proposed commit gate**

Stop and present this proposed commit message before committing:

```text
test: add Aspire contract fixture
```

Do not commit until Deniz approves.

---

### Task 2: Migrate Existing Contract Tests From RIE Client To Aspire HTTP

**Files:**
- Modify: `tests/BadgeSmith.Api.Tests/Functional/HealthContractTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Functional/RoutingContractTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Functional/PackageBadgeContractTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Functional/TestResultsContractTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Http/HttpClientFactoryTests.cs`

**Interfaces:**
- Consumes: `AspireContractFixture.Api` and `AwsTestSeeder.HmacSecret` from Task 1.
- Produces: existing contract tests running through `APIGatewayEmulator` over HTTP and no `AotContract` category on Aspire-backed tests.

- [ ] **Step 1: Replace fixture type and RIE client usage**

In each `tests/BadgeSmith.Api.Tests/Functional/*ContractTests.cs` file, replace the class constructor parameter type:

```csharp
public sealed class HealthContractTests(AspireContractFixture stack)
```

Replace every call to `stack.Lambda.InvokeAsync` with `stack.Api.InvokeAsync`. For example:

```csharp
var response = await stack.Api.InvokeAsync("GET", "/health", ct: TestContext.Current.CancellationToken);
```

- [ ] **Step 2: Remove `AotContract` traits from Aspire-backed classes**

In each functional contract test class, remove this trait:

```csharp
[Trait("Category", TestCategories.AotContract)]
```

Keep these traits:

```csharp
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
```

- [ ] **Step 3: Replace HMAC secret references**

In `tests/BadgeSmith.Api.Tests/Functional/TestResultsContractTests.cs`, replace:

```csharp
BadgeSmithStackFixture.HmacSecret
```

with:

```csharp
AwsTestSeeder.HmacSecret
```

- [ ] **Step 4: Add missing unit category to HTTP factory tests**

At the top of `tests/BadgeSmith.Api.Tests/Http/HttpClientFactoryTests.cs`, add:

```csharp
using BadgeSmith.Api.Tests.Testing;
```

Add this attribute to the test class:

```csharp
[Trait("Category", TestCategories.Unit)]
```

- [ ] **Step 5: Run migrated contract tests**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category=Functional"
```

Expected: the functional contract tests start the Aspire AppHost and pass through `APIGatewayEmulator`. If WireMock is not yet available to the AppHost route, package tests fail with real-upstream or connection errors; record the exact failure and continue to Task 3.

- [ ] **Step 6: Proposed commit gate**

Stop and present this proposed commit message before committing:

```text
test: migrate contract tests to Aspire HTTP
```

Do not commit until Deniz approves.

---

### Task 3: Make Mock Upstreams Available Without AppHost Pollution

**Files:**
- Modify: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AspireContractFixture.cs`
- Modify: `src/BadgeSmith.Host/Program.cs`
- Keep: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/**`

**Interfaces:**
- Consumes: `HTTP_NUGET_BASE_URL` and `HTTP_GITHUB_BASE_URL` support already implemented in `HttpClientFactory`.
- Produces: deterministic WireMock upstreams for Aspire contract tests without adding test mappings to `src/BadgeSmith.Host/Program.cs`.

- [ ] **Step 1: Add Testcontainers WireMock only to the Aspire fixture**

Modify `AspireContractFixture.cs` to add these `using` statements:

```csharp
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
```

Add a private field:

```csharp
private IContainer? _wiremock;
```

Before `DistributedApplicationTestingBuilder.CreateAsync<Projects.BadgeSmith_Host>()`, start WireMock:

```csharp
var wiremockDir = Path.Combine(AppContext.BaseDirectory, "Testing", "Infrastructure", "wiremock");
_wiremock = new ContainerBuilder("wiremock/wiremock:3.9.1")
    .WithBindMount(wiremockDir, "/home/wiremock", AccessMode.ReadOnly)
    .WithPortBinding(8080, assignRandomHostPort: true)
    .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
        request.ForPort(8080).ForPath("/__admin/health")))
    .Build();

await _wiremock.StartAsync().ConfigureAwait(false);
var wiremockBaseUrl = $"http://{_wiremock.Hostname}:{_wiremock.GetMappedPublicPort(8080)}";
```

Set upstream override environment variables before creating the AppHost builder:

```csharp
Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", wiremockBaseUrl + "/nuget/");
Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", wiremockBaseUrl + "/github/");
```

In `DisposeAsync`, clear those variables before disposing WireMock:

```csharp
Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", null);
Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", null);
```

- [ ] **Step 2: Pass optional upstream overrides through the AppHost**

In `src/BadgeSmith.Host/Program.cs`, change the `badgeSmithApi` declaration from a fluent expression to an assignable resource variable and pass through optional upstream environment variables. After the existing `badgeSmithApi` assignment, add:

```csharp
var httpNuGetBaseUrl = Environment.GetEnvironmentVariable("HTTP_NUGET_BASE_URL");
if (!string.IsNullOrWhiteSpace(httpNuGetBaseUrl))
{
    badgeSmithApi.WithEnvironment("HTTP_NUGET_BASE_URL", httpNuGetBaseUrl);
}

var httpGitHubBaseUrl = Environment.GetEnvironmentVariable("HTTP_GITHUB_BASE_URL");
if (!string.IsNullOrWhiteSpace(httpGitHubBaseUrl))
{
    badgeSmithApi.WithEnvironment("HTTP_GITHUB_BASE_URL", httpGitHubBaseUrl);
}
```

This AppHost change is not WireMock-specific; it forwards generic HTTP base URL overrides that already exist in the API project.

- [ ] **Step 3: Dispose WireMock**

In `DisposeAsync`, dispose WireMock after `_app`:

```csharp
if (_wiremock is not null)
{
    await _wiremock.DisposeAsync().ConfigureAwait(false);
}
```

- [ ] **Step 4: Run package contracts**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~PackageBadgeContractTests"
```

Expected: NuGet and GitHub package contract tests pass against WireMock.

- [ ] **Step 5: Proposed commit gate**

Stop and present this proposed commit message before committing:

```text
test: use WireMock with Aspire contract fixture
```

Do not commit until Deniz approves.

---

### Task 4: Close Contract Matrix Gaps

**Files:**
- Modify: `tests/BadgeSmith.Api.Tests/Functional/PackageBadgeContractTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Functional/TestResultsContractTests.cs`
- Add: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/github-versions-403.json`
- Add: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/github-versions-404.json`
- Add: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/github-versions-empty.json`
- Modify: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AwsTestSeeder.cs`

**Interfaces:**
- Consumes: `stack.Api.InvokeAsync` from Task 1.
- Produces: coverage for valid NuGet version range, GitHub 403/404/empty, GitHub ETag to 304, test-result `Last-Modified`, future timestamp rejection, and redirect cache headers.

- [ ] **Step 1: Add GitHub WireMock mappings**

Create `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/github-versions-403.json`:

```json
{
  "request": { "method": "GET", "urlPath": "/github/orgs/forbidden-org/packages/nuget/any.pkg/versions" },
  "response": { "status": 403, "jsonBody": { "message": "Forbidden" } }
}
```

Create `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/github-versions-404.json`:

```json
{
  "request": { "method": "GET", "urlPath": "/github/orgs/test-org/packages/nuget/missing.pkg/versions" },
  "response": { "status": 404, "jsonBody": { "message": "Not Found" } }
}
```

Create `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/mappings/github-versions-empty.json`:

```json
{
  "request": { "method": "GET", "urlPath": "/github/orgs/test-org/packages/nuget/empty.pkg/versions" },
  "response": {
    "status": 200,
    "headers": { "Content-Type": "application/json" },
    "jsonBody": []
  }
}
```

- [ ] **Step 2: Seed forbidden org secret mapping**

In `AwsTestSeeder.CreateTablesAndSecretsAsync`, add:

```csharp
await CreateSecretAsync(secrets, "badgesmith/github/forbidden-org/package", "dummy-github-pat");
await PutSecretMappingAsync(dynamo, "forbidden-org", "package", "badgesmith/github/forbidden-org/package");
```

- [ ] **Step 3: Add package contract tests**

In `PackageBadgeContractTests.cs`, add these tests:

```csharp
[Fact]
public async Task NuGetBadge_WithValidVersionRange_Should_ReturnMatchingVersion()
{
    var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?version=%5B4.0.0%2C5.0.0%29", ct: TestContext.Current.CancellationToken);

    Assert.Equal(200, r.StatusCode);
    Assert.Contains("\"message\":\"4.0.2\"", r.Body, StringComparison.Ordinal);
}

[Fact]
public async Task GitHubBadge_UpstreamForbidden_Should_Return403()
{
    var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/forbidden-org/any.pkg", ct: TestContext.Current.CancellationToken);

    Assert.Equal(403, r.StatusCode);
}

[Fact]
public async Task GitHubBadge_UpstreamMissingPackage_Should_Return404()
{
    var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/missing.pkg", ct: TestContext.Current.CancellationToken);

    Assert.Equal(404, r.StatusCode);
}

[Fact]
public async Task GitHubBadge_UpstreamEmptyVersions_Should_Return404()
{
    var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/empty.pkg", ct: TestContext.Current.CancellationToken);

    Assert.Equal(404, r.StatusCode);
}

[Fact]
public async Task GitHubBadge_Should_Honor_IfNoneMatch()
{
    var first = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg", ct: TestContext.Current.CancellationToken);
    Assert.Equal(200, first.StatusCode);

    var second = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg",
        new Dictionary<string, string>(StringComparer.Ordinal) { ["if-none-match"] = first.Headers["ETag"] },
        ct: TestContext.Current.CancellationToken);

    Assert.Equal(304, second.StatusCode);
}
```

- [ ] **Step 4: Add test-result contract assertions**

In `TestResultsContractTests.cs`, extend `Ingestion_Then_Badge_RoundTrip` after the `ETag` assertion:

```csharp
Assert.True(badge.Headers.ContainsKey("Last-Modified"));
```

Add a future timestamp test:

```csharp
[Fact]
public async Task Ingestion_Should_Reject_FutureTimestamp_With400()
{
    var testCase = CreateCase("future-timestamp", 9);
    var body = testCase.CreatePayload();
    var (sig, ts, nonce) = HmacTestSigner.Sign(body, AwsTestSeeder.HmacSecret,
        timestamp: DateTimeOffset.UtcNow.AddMinutes(10));
    var headers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["x-signature"] = sig,
        ["x-timestamp"] = ts,
        ["x-nonce"] = nonce,
    };

    var post = await stack.Api.InvokeAsync("POST", testCase.IngestPath, headers, body, TestContext.Current.CancellationToken);

    Assert.Equal(400, post.StatusCode);
}
```

Extend `Redirect_Should_Return302_WithLocation`:

```csharp
Assert.True(redirect.Headers.ContainsKey("Cache-Control"));
Assert.Contains("public", redirect.Headers["Cache-Control"], StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 5: Run focused contract tests**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category=Functional"
```

Expected: all functional contract tests pass. If a new test exposes a real production bug, keep the current behavior pinned only if Deniz agrees; otherwise stop before production-code edits.

- [ ] **Step 6: Proposed commit gate**

Stop and present this proposed commit message before committing:

```text
test: close Aspire contract coverage gaps
```

Do not commit until Deniz approves.

---

### Task 5: Remove RIE-Specific Test Infrastructure And Package References

**Files:**
- Delete: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/LambdaRieClient.cs`
- Delete or replace: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/BadgeSmithStackFixture.cs`
- Modify: `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj`
- Modify: `.superpowers/sdd/task-10-report.md`
- Delete: `docs/research/baselines/2026-07-02-pre-iteration.json`

**Interfaces:**
- Consumes: migrated tests from Tasks 1-4.
- Produces: no active RIE-specific test code or stale RIE baseline file.

- [ ] **Step 1: Delete RIE client**

Delete:

```text
tests/BadgeSmith.Api.Tests/Testing/Infrastructure/LambdaRieClient.cs
```

- [ ] **Step 2: Remove old fixture only after `AspireContractFixture` is in use**

Delete:

```text
tests/BadgeSmith.Api.Tests/Testing/Infrastructure/BadgeSmithStackFixture.cs
```

If the file still contains reusable non-RIE WireMock code after Tasks 1-4, move that code into `AspireContractFixture.cs` first, then delete the old file.

- [ ] **Step 3: Remove unused packages with CLI commands**

Run this search first:

```powershell
rg -n "Amazon\.Lambda\.TestUtilities|Amazon\.Lambda\.APIGatewayEvents|Testcontainers\.LocalStack|LocalStackContainer" tests/BadgeSmith.Api.Tests
```

Expected: no matches after `LambdaRieClient.cs` and `BadgeSmithStackFixture.cs` are deleted.

Then remove the unused package references:

```powershell
dotnet remove "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" package Amazon.Lambda.TestUtilities
dotnet remove "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" package Amazon.Lambda.APIGatewayEvents
dotnet remove "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" package Testcontainers.LocalStack
```

Keep `Testcontainers` while WireMock is test-owned.

- [ ] **Step 4: Remove stale baseline file**

Delete:

```text
docs/research/baselines/2026-07-02-pre-iteration.json
```

- [ ] **Step 5: Update Task 10 report as superseded**

In `.superpowers/sdd/task-10-report.md`, change the status line to:

```markdown
Status: SUPERSEDED by `docs/plans/2026-07-04-rie-free-aspire-localstack-redesign.md`
```

Add this note under `Concerns / deviations`:

```markdown
- The RIE-generated mock baseline is intentionally removed by the RIE-free redesign. New baselines must be generated through LocalStack.
```

- [ ] **Step 6: Search for remaining RIE test references**

Run:

```powershell
rg -n "LambdaRieClient|LambdaHttpResponse|BadgeSmithStackFixture|2015-03-31/functions/function/invocations" tests/BadgeSmith.Api.Tests
```

Expected: no matches.

- [ ] **Step 7: Verify tests and build**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category!=AotContract"
dotnet build --configuration Release
slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"
```

Expected: test and build commands pass with zero warnings; Slopwatch reports zero new issues.

- [ ] **Step 8: Proposed commit gate**

Stop and present this proposed commit message before committing:

```text
test: remove RIE contract infrastructure
```

Do not commit until Deniz approves.

---

### Task 6: Make k6 HTTP-Only

**Files:**
- Modify: `scripts/k6-perf-test.js`

**Interfaces:**
- Consumes: a normal HTTP endpoint in `K6_API_URL`.
- Produces: k6 scenario with no RIE mode, no API Gateway event envelope, and no Lambda response-envelope projection.

- [ ] **Step 1: Remove target mode parsing**

Delete this block from `scripts/k6-perf-test.js`:

```javascript
const TARGET_MODE = __ENV.K6_TARGET_MODE || "http"; // http | rie
if (TARGET_MODE !== "http" && TARGET_MODE !== "rie") {
  throw new Error(`Invalid K6_TARGET_MODE: "${TARGET_MODE}". Must be "http" or "rie".`);
}
```

- [ ] **Step 2: Replace `invoke` with HTTP-only implementation**

Replace the whole `invoke` function with:

```javascript
function invoke(method, path, headers, params) {
  const k6Params = Object.assign({}, params || {});
  if (headers && Object.keys(headers).length > 0) {
    k6Params.headers = headers;
  }

  return http.request(method, `${BASE_URL}${path}`, null, k6Params);
}
```

- [ ] **Step 3: Replace status checks**

In `scripts/k6-perf-test.js`, replace every occurrence of:

```javascript
(r.lambdaStatus || r.status)
```

with:

```javascript
r.status
```

Replace this expression:

```javascript
(response.lambdaStatus || response.status) >= 500
```

with:

```javascript
response.status >= 500
```

Replace this expression:

```javascript
(response.lambdaStatus || response.status) >= 400
```

with:

```javascript
response.status >= 400
```

- [ ] **Step 4: Remove RIE comments and projection text**

Remove comments that describe RIE, API Gateway event wrapping, Lambda response envelopes, or host RIE response timing.

- [ ] **Step 5: Syntax-check the script**

Run:

```powershell
node --check "scripts/k6-perf-test.js"
```

Expected: syntax check exits successfully. If Node is unavailable, run `k6 inspect scripts/k6-perf-test.js` if installed and record the command used.

- [ ] **Step 6: Search for RIE in k6 script**

Run:

```powershell
rg -n "rie|RIE|K6_TARGET_MODE|2015-03-31|functions/function/invocations|lambdaStatus" scripts/k6-perf-test.js
```

Expected: no matches.

- [ ] **Step 7: Proposed commit gate**

Stop and present this proposed commit message before committing:

```text
fix(scripts): make k6 scenario HTTP-only
```

Do not commit until Deniz approves.

---

### Task 7: Spike LocalStack Lambda Image HTTP Target

**Files:**
- Create: `docs/research/2026-07-04-localstack-lambda-image-spike.md`

**Interfaces:**
- Consumes: Lambda image produced by `src/BadgeSmith.Api/Dockerfile` and LocalStack Lambda v2 with Docker socket access.
- Produces: verified decision for LocalStack API Gateway v2 or Function URL benchmark target.

- [ ] **Step 1: Prepare a scratch output file for command evidence**

Run:

```bash
mkdir -p artifacts
: > artifacts/localstack-lambda-image-spike.log
```

Append the commands and important outputs from the following steps to `artifacts/localstack-lambda-image-spike.log` as you run them.

- [ ] **Step 2: Build the Lambda image**

Run:

```powershell
docker build -f "src/BadgeSmith.Api/Dockerfile" --target lambda-image -t badge-smith:localstack-spike .
```

Expected: image builds successfully for host architecture.

- [ ] **Step 3: Start LocalStack with Docker socket access**

Run from a shell that can mount Docker socket:

```bash
docker rm -f bs-ls-spike >/dev/null 2>&1 || true
docker run -d --name bs-ls-spike \
  -p 4566:4566 \
  -e DEBUG=1 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  localstack/localstack:4.6
```

Expected: `curl -fsS http://localhost:4566/_localstack/health` succeeds within 60 seconds.

- [ ] **Step 4: Seed DynamoDB and Secrets Manager**

Run:

```bash
bash scripts/perf-baseline-seed.sh bridge
```

Expected: DynamoDB tables and Secrets Manager entries exist in LocalStack.

- [ ] **Step 5: Try Function URL first because the LocalStack docs document it as the direct HTTP Lambda path**

Run the LocalStack Lambda creation commands supported by the installed LocalStack version. Start with a direct local image reference:

```bash
aws --endpoint-url http://localhost:4566 lambda create-function \
  --function-name badge-smith-spike \
  --package-type Image \
  --code ImageUri=badge-smith:localstack-spike \
  --role arn:aws:iam::000000000000:role/lambda-role \
  --timeout 20 \
  --memory-size 512 \
  --environment 'Variables={DOTNET_ENVIRONMENT=Production,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test,AWS_REGION=us-east-1,AWS_DEFAULT_REGION=us-east-1,AWS_RESOURCE_TEST_RESULTS_TABLE=badge-smith-test-result,AWS_RESOURCE_NONCE_TABLE=badge-smith-hmac-nonce,AWS_RESOURCE_ORG_SECRETS_TABLE=badge-smith-github-org-secrets,HTTP_NUGET_BASE_URL=https://api.nuget.org/,HTTP_GITHUB_BASE_URL=https://api.github.com/}'
aws --endpoint-url http://localhost:4566 lambda wait function-active-v2 --function-name badge-smith-spike
aws --endpoint-url http://localhost:4566 lambda create-function-url-config --function-name badge-smith-spike --auth-type NONE
```

If LocalStack rejects the local image reference, record the error in the research note and switch to LocalStack ECR following the sample linked from LocalStack's Lambda docs. Do not use RIE as a fallback.

- [ ] **Step 6: Validate Function URL event shape**

Call the Function URL returned by Step 5:

```bash
FUNCTION_URL="$(aws --endpoint-url http://localhost:4566 lambda get-function-url-config --function-name badge-smith-spike --query FunctionUrl --output text)"
curl -i "${FUNCTION_URL%/}/health"
curl -i "${FUNCTION_URL%/}/badges/packages/nuget/Newtonsoft.Json"
curl -i "${FUNCTION_URL%/}/badges/packages/github/localstack-dotnet/localstack.client?prerelease=true"
```

Expected: `/health` returns `200`. The package routes return either successful badges or expected upstream/credential failures that prove routing and request shape work. Record status codes and response bodies in the research note.

- [ ] **Step 7: Try API Gateway v2 only if Function URL works or if Function URL event shape is insufficient**

Create an HTTP API, Lambda proxy integration, proxy route, and default stage with AWS CLI `apigatewayv2` commands. Use the Lambda ARN from:

```bash
aws --endpoint-url http://localhost:4566 lambda get-function --function-name badge-smith-spike
```

Validate the API endpoint with:

```bash
API_ID="$(aws --endpoint-url http://localhost:4566 apigatewayv2 get-apis --query 'Items[?Name==`badge-smith-spike`].ApiId | [0]' --output text)"
curl -i "http://${API_ID}.execute-api.localhost.localstack.cloud:4566/health"
```

Expected: `/health` returns `200`. Record the exact API Gateway v2 commands that worked in the research note.

- [ ] **Step 8: Create the research note and decide benchmark target**

Create `docs/research/2026-07-04-localstack-lambda-image-spike.md` with observed environment values, command excerpts from `artifacts/localstack-lambda-image-spike.log`, and one of these decision blocks:

```markdown
## Decision

- Selected target: Function URL
- Reason: Function URL produced valid route paths, query strings, headers, and bodies for BadgeSmith's routes.
```

or:

```markdown
## Decision

- Selected target: API Gateway v2
- Reason: Function URL event shape was insufficient, while API Gateway v2 produced valid BadgeSmith route behavior.
```

or:

```markdown
## Decision

- Selected target: none
- Reason: LocalStack failed to execute the published BadgeSmith Lambda image reliably. Do not reintroduce RIE; use Aspire Testing for contract coverage and deployed AWS for AOT artifact verification.
```

- [ ] **Step 9: Proposed commit gate**

Stop and present this proposed commit message before committing:

```text
docs(research): record LocalStack Lambda image spike
```

Do not commit until Deniz approves.

---

### Task 8: Rewrite Local Benchmark Harness Around LocalStack HTTP Target

**Files:**
- Modify: `scripts/perf-baseline.sh`
- Modify: `scripts/perf-baseline-seed.sh`
- Modify: `docs/research/baselines/*.json` after successful run

**Interfaces:**
- Consumes: selected LocalStack HTTP target from Task 7 and HTTP-only k6 script from Task 6.
- Produces: mock and real local benchmark baselines that run through LocalStack and use seeded Secrets Manager credentials.

- [ ] **Step 1: Remove RIE-specific variables and comments**

In `scripts/perf-baseline.sh`, remove comments and variables that describe local Lambda RIE serialization. Keep `K6_VUS` and `K6_DURATION`, but do not set `K6_TARGET_MODE` anywhere.

- [ ] **Step 2: Start LocalStack with Docker socket access**

Replace the existing LocalStack startup command with one that mounts Docker socket:

```bash
docker run -d --name bs-perf-ls --network "$NET" --network-alias localstack -p 4566 \
  -e DEBUG=1 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  localstack/localstack:4.6 >/dev/null
```

- [ ] **Step 3: Seed credentials through LocalStack only**

Keep `scripts/perf-baseline-seed.sh` as the only credential import path. In `scripts/perf-baseline.sh`, keep this check for real upstream:

```bash
if [[ "$UPSTREAM" == "real" && -z "${GITHUB_TOKEN:-}" ]]; then
  echo "GITHUB_TOKEN is required when --upstream real so the seeder can populate LocalStack Secrets Manager" >&2
  exit 1
fi
```

Do not pass `GITHUB_TOKEN` to the Lambda container or LocalStack function environment.

- [ ] **Step 4: Deploy the Lambda image to the Task 7 target**

Add shell functions that implement the selected Task 7 target. If Task 7 selected Function URL, add a function named `create_function_url_target` that creates the Lambda function, waits for `function-active-v2`, creates Function URL config, and writes the URL to `K6_API_URL`. If Task 7 selected API Gateway v2, add a function named `create_apigateway_v2_target` with the exact commands recorded in the spike note.

The function environment must include:

```bash
DOTNET_ENVIRONMENT=Production
AWS_ACCESS_KEY_ID=test
AWS_SECRET_ACCESS_KEY=test
AWS_REGION=us-east-1
AWS_DEFAULT_REGION=us-east-1
AWS_RESOURCE_TEST_RESULTS_TABLE=badge-smith-test-result
AWS_RESOURCE_NONCE_TABLE=badge-smith-hmac-nonce
AWS_RESOURCE_ORG_SECRETS_TABLE=badge-smith-github-org-secrets
HTTP_NUGET_BASE_URL=$NUGET_URL
HTTP_GITHUB_BASE_URL=$GITHUB_URL
```

- [ ] **Step 5: Run k6 in HTTP mode**

Replace both k6 invocations with commands that omit `K6_TARGET_MODE`:

```bash
"${K6[@]}" run --summary-export "$K6_JSON_ARG" -e K6_API_URL="$K6_API_URL" \
  -e K6_VUS="$VUS" -e K6_DURATION="$DURATION" "$K6_SCRIPT_ARG" > "$K6_LOG" 2>&1 || K6_EXIT=$?
```

For Windows k6:

```bash
K6_COMMAND="k6.exe run --summary-export $K6_JSON_ARG -e K6_API_URL=$K6_API_URL -e K6_VUS=$VUS -e K6_DURATION=$DURATION $K6_SCRIPT_ARG"
```

- [ ] **Step 6: Normalize baseline memory schema**

In the Python JSON writer inside `scripts/perf-baseline.sh`, emit memory as MB values:

```python
def kb_to_mb(value):
    return round(int(value) / 1024, 3)

"memory": {"rssIdleMb": rss_idle, "rssPeakMb": kb_to_mb(rss_peak_kb)},
```

If LocalStack cannot attribute RSS to the Lambda worker container, emit:

```python
"memory": {"rssIdleMb": None, "rssPeakMb": None, "source": "not-attributed-localstack"},
```

- [ ] **Step 7: Run mock benchmark smoke**

Run:

```bash
K6_DURATION=10s K6_VUS=1 bash scripts/perf-baseline.sh --label localstack-smoke --upstream mock
```

Expected: k6 checks pass and `docs/research/baselines/$(date -u +%Y-%m-%d)-localstack-smoke.json` is written.

- [ ] **Step 8: Run real benchmark smoke only with available credential**

Run:

```bash
if [[ -z "${GITHUB_TOKEN:-}" ]]; then echo "GITHUB_TOKEN must be set for real upstream smoke" >&2; exit 1; fi
K6_DURATION=10s K6_VUS=1 bash scripts/perf-baseline.sh --label localstack-real-smoke --upstream real
```

Expected: k6 checks pass or fail with a real upstream issue. If checks fail because package names or credentials are invalid, adjust benchmark scenario data to match seeded `localstack-dotnet` package access; do not bypass the seeder.

- [ ] **Step 9: Proposed commit gate**

Stop and present this proposed commit message before committing:

```text
fix(scripts): benchmark through LocalStack HTTP target
```

Do not commit until Deniz approves.

---

### Task 9: Update Documentation And Roadmap Claims

**Files:**
- Modify: `docs/ROADMAP.md`
- Modify or create: `tests/BadgeSmith.Api.Tests/README.md`
- Modify: `docs/plans/2026-07-02-iteration0-aot-contract-tier-design.md`
- Modify: `docs/plans/2026-07-02-iteration0-aot-contract-tier-plan.md`

**Interfaces:**
- Consumes: verified state from Tasks 1-8.
- Produces: documentation that points future workers at the RIE-free design and no longer instructs them to continue the old RIE plan.

- [ ] **Step 1: Add test project README**

Create or update `tests/BadgeSmith.Api.Tests/README.md` with:

```markdown
# BadgeSmith.Api.Tests

The test project uses xUnit v3 on VSTest.

## Categories

- `Category=Unit`: in-process unit tests.
- `Category=Integration`: tests requiring Aspire, LocalStack, WireMock, or other infrastructure.
- `Category=Functional`: HTTP contract tests that exercise BadgeSmith routes.

`Category=AotContract` is reserved for a future RIE-free AOT artifact smoke tier. The Aspire-backed contract tests do not use this category.

## Contract Tests

Contract tests start `src/BadgeSmith.Host` through Aspire Testing and call `APIGatewayEmulator` over HTTP. They do not use Lambda RIE or the Lambda invocation endpoint.

## Benchmark Tests

k6 benchmark scripts are not contract tests. Local benchmark runs target LocalStack and seed DynamoDB plus Secrets Manager before invoking package routes.
```

- [ ] **Step 2: Add supersession notes to old plans**

At the top of `docs/plans/2026-07-02-iteration0-aot-contract-tier-design.md`, after the title, add:

```markdown
> Superseded note (2026-07-04): RIE-dependent contract and benchmark paths are superseded by `2026-07-04-rie-free-aspire-localstack-redesign.md`. Keep this document for historical context only.
```

At the top of `docs/plans/2026-07-02-iteration0-aot-contract-tier-plan.md`, after the title, add:

```markdown
> Superseded note (2026-07-04): Do not continue this RIE-based task list. Use `2026-07-04-rie-free-aspire-localstack-implementation-plan.md` for implementation.
```

- [ ] **Step 3: Update roadmap only with verified facts**

In `docs/ROADMAP.md`, update Iteration 0 status to mention:

```markdown
Iteration 0 was redirected on 2026-07-04 from a RIE-backed AOT contract tier to a RIE-free design: Aspire Testing for contract/integration coverage and LocalStack for local benchmark execution.
```

Do not claim the LocalStack benchmark is complete unless Task 8 verification passed.

- [ ] **Step 4: Search for stale RIE instructions**

Run:

```powershell
rg -n "RIE|Runtime Interface Emulator|K6_TARGET_MODE=rie|2015-03-31/functions/function/invocations|LambdaRieClient" docs tests scripts
```

Expected: matches only in historical superseded notes or research discussion, not in active instructions.

- [ ] **Step 5: Proposed commit gate**

Stop and present this proposed commit message before committing:

```text
docs: document RIE-free test and benchmark workflow
```

Do not commit until Deniz approves.

---

### Task 10: Final Verification Pass

**Files:**
- No intended source edits.

**Interfaces:**
- Consumes: all previous tasks.
- Produces: evidence that the RIE-free branch is ready for review.

- [ ] **Step 1: Full repository build**

Run:

```powershell
dotnet build --configuration Release
```

Expected: `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 2: Unit tests**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category=Unit"
```

Expected: all unit tests pass.

- [ ] **Step 3: Functional contract tests**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category=Functional"
```

Expected: Aspire-backed contract tests pass.

- [ ] **Step 4: Non-AOT tests**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category!=AotContract"
```

Expected: all non-AOT tests pass.

- [ ] **Step 5: k6 script syntax**

Run:

```powershell
node --check "scripts/k6-perf-test.js"
```

Expected: script syntax is valid.

- [ ] **Step 6: LocalStack benchmark smoke**

Run:

```bash
K6_DURATION=10s K6_VUS=1 bash scripts/perf-baseline.sh --label final-localstack-smoke --upstream mock
```

Expected: k6 checks pass and a baseline JSON is written. If Docker or LocalStack is unavailable on the machine, record that blocker instead of claiming benchmark verification.

- [ ] **Step 7: Slopwatch**

Run:

```powershell
slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"
```

Expected: `0 issue(s) found` or only previously accepted baseline issues.

- [ ] **Step 8: Final RIE search**

Run:

```powershell
rg -n "LambdaRieClient|K6_TARGET_MODE=rie|2015-03-31/functions/function/invocations|Runtime Interface Emulator" tests scripts src docs
```

Expected: no matches in active code or scripts. Historical docs may mention RIE only as superseded context.

- [ ] **Step 9: Proposed commit gate**

If the final verification required doc or script edits, stop and present a proposed commit message. If no edits were made, report verification evidence and current uncommitted files.
