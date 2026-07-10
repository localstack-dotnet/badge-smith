# PR #5 Runtime Security And URL Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert malformed HMAC digests into HTTP 401 responses and give both ingestion commands one safe, white-label base URL contract with encoded route values.

**Architecture:** Decode only exact-length SHA-256 digests into a fixed-size buffer and retain constant-time comparison for valid inputs. Add a focused tool URL builder that validates one base URI, preserves ports and path prefixes, and appends independently escaped route segments for ingest, badge, and redirect URLs.

**Tech Stack:** .NET 10, `System.Security.Cryptography`, `System.Buffers.OperationStatus`, `Uri`, Spectre.Console.Cli, xUnit v3, Moq, Aspire contract tests.

## Global Constraints

- Follow `docs/superpowers/specs/2026-07-10-pr5-merge-readiness-remediation-design.md`.
- Malformed, odd-length, short, long, or non-hex HMAC input returns `InvalidSignature` and never marks a nonce.
- Preserve constant-time comparison for an exact 32-byte decoded SHA-256 digest.
- Require an absolute HTTP or HTTPS base URL with no credentials, query, or fragment.
- Preserve custom ports and path prefixes and remove only trailing `/` characters.
- Encode platform, owner, repository, and branch independently with `Uri.EscapeDataString`.
- Do not provide a default BadgeSmith deployment URL.
- Do not add compatibility aliases for `api_domain` or `--api-domain`.
- Keep the HMAC payload and signature formats unchanged.

---

## File Structure

- Modify `src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs`: own exception-free digest parsing and constant-time comparison.
- Modify `tests/BadgeSmith.Api.Tests/Security/HmacAuthenticationServiceTests.cs`: own malformed digest and nonce unit coverage.
- Modify `tests/BadgeSmith.Api.Tests/Functional/TestResultsContractTests.cs`: own the HTTP 401 contract.
- Create `tools/Infrastructure/BadgeSmithUrlBuilder.cs`: own base URL validation, normalization, and route construction.
- Create `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithUrlBuilderTests.cs`: own URL safety, path-prefix, port, and encoding coverage.
- Modify `tools/Commands/BadgeUpdateCommand.cs`: consume `--base-url` and the shared URL builder.
- Modify `tools/Commands/TestIngestCommand.cs`: remove its production-domain default and consume the shared URL builder.
- Modify `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`: verify emitted URLs from the process boundary.
- Modify `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`: verify emitted URLs from the in-process boundary.

### Task 1: Harden HMAC Parsing And Introduce The White-Label URL Contract

**Files:**
- Create: `tools/Infrastructure/BadgeSmithUrlBuilder.cs`
- Create: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithUrlBuilderTests.cs`
- Modify: `src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs:123-145`
- Modify: `tests/BadgeSmith.Api.Tests/Security/HmacAuthenticationServiceTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Functional/TestResultsContractTests.cs:147-158`
- Modify: `tools/Commands/BadgeUpdateCommand.cs`
- Modify: `tools/Commands/TestIngestCommand.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs:69-114`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs:50-102`

**Interfaces:**
- Consumes: `HmacAuthContext.Signature`, existing `InvalidSignature`, existing command settings, and raw base URL strings.
- Produces: `BadgeSmithUrlBuilder.TryCreate(string?, out BadgeSmithUrlBuilder, out string)`, `BuildIngestUrl`, `BuildBadgeUrl`, and `BuildRedirectUrl`, each returning a normalized absolute URL string.

- [ ] **Step 1: Add unit coverage for malformed HMAC digests and nonce behavior**

Add this data and test to `HmacAuthenticationServiceTests`:

```csharp
    public static TheoryData<string> MalformedSignatures => new()
    {
        "sha256=" + new string('z', 64),
        "sha256=" + new string('0', 63),
        "sha256=" + new string('0', 62),
        "sha256=" + new string('0', 66),
    };

    [Theory]
    [MemberData(nameof(MalformedSignatures))]
    public async Task ValidateRequestAsync_Should_Return_Invalid_Signature_Without_Marking_Nonce_When_Signature_Is_Malformed(string signature)
    {
        const string owner = "localstack-dotnet";
        const string repo = "badge-smith";
        const string platform = "linux";
        const string branch = "feature/tools";
        const string secret = "test-secret";
        const string body = "{\"total\":1}";
        var (_, timestamp, nonce) = HmacTestSigner.Sign(body, secret);

        var secretsService = new Mock<IGitHubOrgSecretsService>(MockBehavior.Strict);
        secretsService
            .Setup(service => service.GetGitHubTokenAsync(owner, "TestData", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GithubSecretResult)secret);
        var nonceService = new Mock<INonceService>(MockBehavior.Strict);
        var service = new HmacAuthenticationService(
            secretsService.Object,
            nonceService.Object,
            Mock.Of<ILogger<HmacAuthenticationService>>());

        var result = await service.ValidateRequestAsync(
            new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, nonce, body),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Failure.IsT0);
        secretsService.VerifyAll();
        nonceService.VerifyAll();
    }
```

The existing `ValidateRequestAsync_Should_Not_Mark_Nonce_When_Signature_Is_Invalid` test remains and continues to cover an exact-length but incorrect digest.

- [ ] **Step 2: Change the functional contract from the known 500 to 401**

Replace the malformed-hex functional test with:

```csharp
    [Fact]
    public async Task Ingestion_Should_Return_401_When_Signature_Hex_Is_Malformed()
    {
        var testCase = CreateCase("malformed-hex", 6);
        var body = testCase.CreatePayload();
        var headers = AuthHeaders(body);
        headers["x-signature"] = "sha256=" + new string('z', 64);

        var post = await stack.Api.InvokeAsync(
            "POST",
            testCase.IngestPath,
            headers,
            body,
            TestContext.Current.CancellationToken);

        Assert.Equal(401, post.StatusCode);
    }
```

- [ ] **Step 3: Run the HMAC tests and observe the regression**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~HmacAuthenticationServiceTests"
```

Expected before implementation: each malformed prefixed digest throws `FormatException`, so the new theory fails.

- [ ] **Step 4: Implement exception-free digest decoding**

Add `using System.Buffers;` to `HmacAuthenticationService.cs`, then replace `ValidateHmacSignature` and `ComputeHmacSha256` with:

```csharp
    private static bool ValidateHmacSignature(string providedSignature, string payload, string secret)
    {
        if (!providedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHash = providedSignature.AsSpan(7);
        if (providedHash.Length != 64)
        {
            return false;
        }

        Span<byte> providedHashBytes = stackalloc byte[32];
        var status = Convert.FromHexString(providedHash, providedHashBytes, out var charsConsumed, out var bytesWritten);
        if (status != OperationStatus.Done || charsConsumed != providedHash.Length || bytesWritten != providedHashBytes.Length)
        {
            return false;
        }

        var computedHashBytes = ComputeHmacSha256(payload, secret);
        return CryptographicOperations.FixedTimeEquals(providedHashBytes, computedHashBytes);
    }

    private static byte[] ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        return hmac.ComputeHash(payloadBytes);
    }
```

This uses the .NET 10 `Convert.FromHexString(ReadOnlySpan<char>, Span<byte>, out int, out int)` overload, which reports `OperationStatus.InvalidData` instead of throwing for malformed characters.

- [ ] **Step 5: Run the focused HMAC tests**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~HmacAuthenticationServiceTests"
```

Expected: all HMAC unit tests pass, including malformed signatures and the valid-length incorrect signature.

- [ ] **Step 6: Add URL builder tests before implementation**

Create `BadgeSmithUrlBuilderTests.cs` with:

```csharp
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Tools.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Trait("Category", TestCategories.Unit)]
public sealed class BadgeSmithUrlBuilderTests
{
    [Fact]
    public void Build_Urls_Should_Preserve_Port_And_Path_Prefix_And_Encode_Each_Route_Value()
    {
        var created = BadgeSmithUrlBuilder.TryCreate(
            "http://localhost:9474/prefix/",
            out var urls,
            out var error);

        Assert.True(created, error);
        Assert.Equal(
            "http://localhost:9474/prefix/tests/results/linux/localstack-dotnet/badge%20smith/feature%2Ftools",
            urls.BuildIngestUrl("linux", "localstack-dotnet", "badge smith", "feature/tools"));
        Assert.Equal(
            "http://localhost:9474/prefix/badges/tests/linux/localstack-dotnet/badge%20smith/feature%2Ftools",
            urls.BuildBadgeUrl("linux", "localstack-dotnet", "badge smith", "feature/tools"));
        Assert.Equal(
            "http://localhost:9474/prefix/redirect/test-results/linux/localstack-dotnet/badge%20smith/feature%2Ftools",
            urls.BuildRedirectUrl("linux", "localstack-dotnet", "badge smith", "feature/tools"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("api.example.com")]
    [InlineData("ftp://api.example.com")]
    [InlineData("https://user:password@api.example.com")]
    [InlineData("https://api.example.com?tenant=one")]
    [InlineData("https://api.example.com#badge")]
    public void TryCreate_Should_Reject_Unsafe_Base_Url(string baseUrl)
    {
        var created = BadgeSmithUrlBuilder.TryCreate(baseUrl, out _, out var error);

        Assert.False(created);
        Assert.Contains("Base URL", error, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 7: Run the URL builder tests and observe the missing type**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~BadgeSmithUrlBuilderTests"
```

Expected before implementation: compilation fails because `BadgeSmithUrlBuilder` does not exist.

- [ ] **Step 8: Implement the shared URL builder**

Create `tools/Infrastructure/BadgeSmithUrlBuilder.cs` with:

```csharp
using System.Text;

namespace BadgeSmith.Tools.Infrastructure;

internal sealed class BadgeSmithUrlBuilder
{
    private readonly string _baseUrl;

    private BadgeSmithUrlBuilder(Uri baseUri)
    {
        _baseUrl = baseUri.AbsoluteUri.TrimEnd('/');
    }

    public static bool TryCreate(string? value, out BadgeSmithUrlBuilder builder, out string error)
    {
        builder = null!;
        error = "";

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Base URL is required.";
            return false;
        }

        var trimmedValue = value.Trim();
        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(baseUri.Host))
        {
            error = "Base URL must be an absolute HTTP or HTTPS URL.";
            return false;
        }

        if (!string.IsNullOrEmpty(baseUri.UserInfo))
        {
            error = "Base URL must not contain credentials.";
            return false;
        }

        if (trimmedValue.Contains('?'))
        {
            error = "Base URL must not contain a query string.";
            return false;
        }

        if (trimmedValue.Contains('#'))
        {
            error = "Base URL must not contain a fragment.";
            return false;
        }

        builder = new BadgeSmithUrlBuilder(baseUri);
        return true;
    }

    public string BuildIngestUrl(string platform, string owner, string repository, string branch)
    {
        return BuildUrl("tests", "results", platform, owner, repository, branch);
    }

    public string BuildBadgeUrl(string platform, string owner, string repository, string branch)
    {
        return BuildUrl("badges", "tests", platform, owner, repository, branch);
    }

    public string BuildRedirectUrl(string platform, string owner, string repository, string branch)
    {
        return BuildUrl("redirect", "test-results", platform, owner, repository, branch);
    }

    private string BuildUrl(params string[] segments)
    {
        var url = new StringBuilder(_baseUrl);
        foreach (var segment in segments)
        {
            url.Append('/');
            url.Append(Uri.EscapeDataString(segment));
        }

        return url.ToString();
    }
}
```

- [ ] **Step 9: Run the URL builder tests**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~BadgeSmithUrlBuilderTests"
```

Expected: all URL builder tests pass.

- [ ] **Step 10: Replace badge update's domain option with the base URL contract**

In `BadgeUpdateCommand.ExecuteAsync`, validate the builder after repository parsing:

```csharp
        if (!BadgeSmithUrlBuilder.TryCreate(settings.BaseUrl, out var urls, out var baseUrlError))
        {
            _console.MarkupLine($"[red]{Markup.Escape(baseUrlError)}[/]");
            return ToolExitCodes.ValidationFailure;
        }
```

Replace the three interpolated domain URLs with:

```csharp
        var url = urls.BuildIngestUrl(platform, owner, repo, branch);
        var badgeUrl = urls.BuildBadgeUrl(platform, owner, repo, branch);
        var redirectUrl = urls.BuildRedirectUrl(platform, owner, repo, branch);
```

Replace `ApiDomain` in `BadgeUpdateSettings` with:

```csharp
    [CommandOption("--base-url")]
    [Description("BadgeSmith API base URL.")]
    public string BaseUrl { get; init; } = "";
```

Add this validation override to `BadgeUpdateSettings` while retaining the existing HMAC option until the tooling/workflow plan removes it:

```csharp
    public override ValidationResult Validate()
    {
        return BadgeSmithUrlBuilder.TryCreate(BaseUrl, out _, out var error)
            ? ValidationResult.Success()
            : ValidationResult.Error(error);
    }
```

- [ ] **Step 11: Make tests ingest consume the same URL contract**

At the start of `TestIngestCommand.ExecuteAsync`, add:

```csharp
        if (!BadgeSmithUrlBuilder.TryCreate(settings.BaseUrl, out var urls, out var baseUrlError))
        {
            _console.MarkupLine($"[red]{Markup.Escape(baseUrlError)}[/]");
            return ToolExitCodes.ValidationFailure;
        }
```

Replace the raw URL interpolation with:

```csharp
        var url = urls.BuildIngestUrl(platform, owner, repo, branch);
```

Change `TestIngestSettings.BaseUrl` to have no deployment default:

```csharp
    [CommandOption("--base-url")]
    [Description("BadgeSmith API base URL.")]
    public string BaseUrl { get; init; } = "";
```

Add the base URL check at the start of the existing `Validate` method:

```csharp
        if (!BadgeSmithUrlBuilder.TryCreate(BaseUrl, out _, out var baseUrlError))
        {
            return ValidationResult.Error(baseUrlError);
        }
```

Retain the existing payload-presence validation after this new check.

- [ ] **Step 12: Update process and in-process URL expectations**

In both tooling test files, replace badge update arguments:

```csharp
            "--base-url", "https://api.example.com/prefix/",
            "--hmac-secret", "test-secret",
            "--branch", "feature/tools",
```

In `BadgeSmithToolCommandTests`, assert these exact encoded URLs:

```csharp
        Assert.Contains(
            "https://api.example.com/prefix/tests/results/linux/localstack-dotnet/badge-smith/feature%2Ftools",
            result.Output,
            StringComparison.Ordinal);
        Assert.Contains(
            "https://api.example.com/prefix/badges/tests/linux/localstack-dotnet/badge-smith/feature%2Ftools",
            result.Output,
            StringComparison.Ordinal);
```

In `BadgeSmithToolInProcessTests`, assert the same URLs against the injected console:

```csharp
        Assert.Contains(
            "https://api.example.com/prefix/tests/results/linux/localstack-dotnet/badge-smith/feature%2Ftools",
            console.Output,
            StringComparison.Ordinal);
        Assert.Contains(
            "https://api.example.com/prefix/badges/tests/linux/localstack-dotnet/badge-smith/feature%2Ftools",
            console.Output,
            StringComparison.Ordinal);
```

Change the tests-ingest branch in both files from `Main` to `feature/tools`. In `BadgeSmithToolCommandTests`, assert:

```csharp
        Assert.Contains(
            "https://example.com/tests/results/linux/localstack-dotnet/badgesmith/feature%2Ftools",
            result.Output,
            StringComparison.Ordinal);
```

In `BadgeSmithToolInProcessTests`, assert:

```csharp
        Assert.Contains(
            "https://example.com/tests/results/linux/localstack-dotnet/badgesmith/feature%2Ftools",
            console.Output,
            StringComparison.Ordinal);
```

- [ ] **Step 13: Run focused URL and command tests**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~BadgeSmithUrlBuilderTests|FullyQualifiedName~BadgeSmithToolCommandTests|FullyQualifiedName~BadgeSmithToolInProcessTests"
```

Expected: all selected tests pass; output contains the path prefix and `%2F` branch encoding.

- [ ] **Step 14: Run the functional malformed-signature contract test**

Run with Docker available:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~TestResultsContractTests.Ingestion_Should_Return_401_When_Signature_Hex_Is_Malformed"
```

Expected: the LocalStack-backed test passes with HTTP `401` rather than `500`.

- [ ] **Step 15: Inspect and commit the runtime security and URL change**

Run:

```bash
git diff --check
```

Expected: only HMAC handling, shared URL construction, command URL consumption, and their tests changed.

After presenting the required pre-commit summary and receiving approval, run:

```bash
git add src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs tests/BadgeSmith.Api.Tests/Security/HmacAuthenticationServiceTests.cs tests/BadgeSmith.Api.Tests/Functional/TestResultsContractTests.cs tools/Infrastructure/BadgeSmithUrlBuilder.cs tools/Commands/BadgeUpdateCommand.cs tools/Commands/TestIngestCommand.cs tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithUrlBuilderTests.cs tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs
```

Expected: one focused commit matching the approved boundary.

## Plan Verification

- Non-hex, odd-length, short, and long digests return `InvalidSignature` without nonce use.
- An exact-length incorrect digest still fails authentication.
- The functional contract returns HTTP 401 for malformed hex.
- HTTP and HTTPS base URLs support ports and path prefixes.
- Credentials, query strings, fragments, relative values, and non-HTTP schemes are rejected.
- Platform, owner, repository, and branch are encoded independently.
- Neither command has a default BadgeSmith deployment URL.
- `--api-domain` and `api_domain` do not survive in production code or active tests.
