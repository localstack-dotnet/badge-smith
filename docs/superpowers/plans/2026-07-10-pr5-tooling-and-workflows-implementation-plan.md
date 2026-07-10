# PR #5 Tooling And Workflows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Validate CLI input before I/O, remove the badge HMAC secret from process arguments, make the badge action remotely reusable, and harden all composite-action shell boundaries.

**Architecture:** Spectre settings reject invalid values before command execution, while badge authentication reads one named environment value through the host configuration. The public badge action runs the tool and SDK from `github.action_path`; the internal test action stays workspace-local. Every expression that reaches a shell first crosses a step-level environment boundary.

**Tech Stack:** .NET 10 file-based apps, Spectre.Console.Cli, Microsoft.Extensions.Configuration, xUnit v3, GitHub composite actions, `actions/setup-dotnet@v4`, actionlint.

## Global Constraints

- Follow `docs/superpowers/specs/2026-07-10-pr5-merge-readiness-remediation-design.md`.
- `badge update` reads only `BADGESMITH_HMAC_SECRET`; remove `--hmac-secret` without an alias.
- `update-test-badge` requires `api_base_url` and has no deployment default.
- `update-test-badge` resolves `global.json` and `tools/badgesmith.cs` from `github.action_path`.
- Use `dotnet run --file` on Windows and the executable file path on Unix-like runners.
- Keep `run-dotnet-tests` repository-local and resolve its tool from `github.workspace`.
- Put every action input or GitHub context expression in step-level `env` before shell use.
- Do not duplicate badge URL rendering in action shell code; the CLI step summary is canonical.
- `secrets seed --dry-run` must validate mapping content without a table name or AWS client construction.
- Non-dry-run secret seeding still requires a table name before AWS clients are created.
- Preserve badge update's existing opt-in `--fail-on-error` behavior.

---

## File Structure

- Modify `tools/Commands/BadgeUpdateCommand.cs`: own environment-secret lookup and complete badge argument validation.
- Modify `tools/Commands/TestIngestCommand.cs`: own required-value, exclusive payload-source, and file-existence validation.
- Modify `tools/Commands/SecretsSeedCommand.cs`: separate dry-run mapping validation from table/AWS requirements.
- Modify `tools/Services/BadgeSmithTool.cs`: publish only current CLI examples.
- Modify `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`: test child-process environment and public command contracts.
- Modify `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`: test settings and injected configuration without network or AWS access.
- Create `tests/BadgeSmith.Api.Tests/Tooling/GitHubActionContractTests.cs`: statically verify action paths and shell-expression boundaries.
- Modify `.github/workflows/update-test-badge/action.yml`: own the public remote action contract.
- Modify `.github/workflows/run-dotnet-tests/action.yml`: own the internal test action's safe shell boundary.
- Modify `.github/workflows/ci-cd.yml`: consume `api_base_url` and install .NET in the ARM64 job.
- Modify `.github/workflows/update-test-badge/README.md`: document remote white-label use and the LocalStack.NET example.
- Modify `README.md`: remove action-copy guidance and document only the public integration surface.
- Modify `tools/README.md`: document required base URLs, environment HMAC, and table-free dry-run.

### Task 1: Validate CLI Inputs And Secure Composite Workflows

**Files:**
- Create: `tests/BadgeSmith.Api.Tests/Tooling/GitHubActionContractTests.cs`
- Modify: `tools/Commands/BadgeUpdateCommand.cs`
- Modify: `tools/Commands/TestIngestCommand.cs`
- Modify: `tools/Commands/SecretsSeedCommand.cs`
- Modify: `tools/Services/BadgeSmithTool.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`
- Modify: `.github/workflows/update-test-badge/action.yml`
- Modify: `.github/workflows/run-dotnet-tests/action.yml`
- Modify: `.github/workflows/ci-cd.yml`
- Modify: `.github/workflows/update-test-badge/README.md`
- Modify: `README.md`
- Modify: `tools/README.md`

**Interfaces:**
- Consumes: `BadgeSmithUrlBuilder`, `IConfiguration`, `github.action_path`, `github.workspace`, action inputs, and `global.json` SDK `10.0.301`.
- Produces: `BADGESMITH_HMAC_SECRET` badge authentication, required `api_base_url`, safe Windows/Unix action invocations, deterministic CLI validation failures, and table-free secret mapping dry-runs.

- [ ] **Step 1: Add child-process environment support to command tests**

Add `using BadgeSmith.Tools.Infrastructure;` to `BadgeSmithToolCommandTests.cs` so the process-boundary assertions use the shared exit-code constants.

Replace the existing `RunToolAsync` helper in `BadgeSmithToolCommandTests` with these two overloads:

```csharp
    private static Task<ToolRunResult> RunToolAsync(params string[] arguments)
    {
        return RunToolAsync(new Dictionary<string, string?>(StringComparer.Ordinal), arguments);
    }

    private static async Task<ToolRunResult> RunToolAsync(
        IReadOnlyDictionary<string, string?> environment,
        params string[] arguments)
    {
        var root = FindRepositoryRoot();
        var toolPath = Path.Combine(root, "tools", "badgesmith.cs");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var (name, value) in environment)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--file");
        startInfo.ArgumentList.Add(toolPath);
        startInfo.ArgumentList.Add("--");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process.");
        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        return new ToolRunResult(process.ExitCode, stdout + stderr);
    }
```

- [ ] **Step 2: Change badge command tests to the environment-secret contract**

In `BadgeSmithToolCommandTests`, replace the badge dry-run invocation with:

```csharp
        var result = await RunToolAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["BADGESMITH_HMAC_SECRET"] = "test-secret",
            },
            "badge", "update",
            "--platform", "Linux",
            "--test-passed", "2",
            "--test-failed", "0",
            "--test-skipped", "1",
            "--test-url-html", "https://example.com/tests",
            "--commit-sha", "abc123",
            "--run-id", "42",
            "--repository", "localstack-dotnet/badge-smith",
            "--server-url", "https://github.com",
            "--base-url", "https://api.example.com/prefix/",
            "--branch", "feature/tools",
            "--dry-run");
```

Keep the existing success and secret-redaction assertions, with encoded path expectations established in the runtime URL plan.

Add this missing-secret process test:

```csharp
    [Fact]
    public async Task BadgeUpdate_Should_Return_Validation_When_Hmac_Environment_Variable_Is_Missing()
    {
        var result = await RunToolAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["BADGESMITH_HMAC_SECRET"] = null,
            },
            "badge", "update",
            "--platform", "Linux",
            "--test-passed", "1",
            "--test-failed", "0",
            "--test-skipped", "0",
            "--commit-sha", "abc123",
            "--run-id", "42",
            "--repository", "localstack-dotnet/badge-smith",
            "--server-url", "https://github.com",
            "--base-url", "https://api.example.com",
            "--branch", "main",
            "--dry-run");

        Assert.Equal(ToolExitCodes.ValidationFailure, result.ExitCode);
        Assert.Contains("BADGESMITH_HMAC_SECRET", result.Output, StringComparison.Ordinal);
    }
```

- [ ] **Step 3: Change the in-process badge test to injected configuration**

Add `using BadgeSmith.Tools.Commands;` to `BadgeSmithToolInProcessTests` and replace its badge invocation with:

```csharp
        var exitCode = await BadgeSmithTool.RunAsync([
            "badge", "update",
            "--platform", "Linux",
            "--test-passed", "2",
            "--test-failed", "0",
            "--test-skipped", "1",
            "--test-url-html", "https://example.com/tests",
            "--commit-sha", "abc123",
            "--run-id", "42",
            "--repository", "localstack-dotnet/badge-smith",
            "--server-url", "https://github.com",
            "--base-url", "https://api.example.com/prefix/",
            "--branch", "feature/tools",
            "--dry-run",
        ], builder => builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("BADGESMITH_HMAC_SECRET", "test-secret"),
        ]), console);
```

Keep the assertions for success, encoded URLs, and absence of `test-secret` from output.

- [ ] **Step 4: Add settings-level validation coverage**

Add these tests and helper to `BadgeSmithToolInProcessTests`:

```csharp
    [Fact]
    public void TestsIngestSettings_Should_Reject_Each_Required_Value_When_Value_Is_Empty()
    {
        var invalidSettings = new[]
        {
            CreateTestIngestSettings(baseUrl: ""),
            CreateTestIngestSettings(owner: ""),
            CreateTestIngestSettings(repo: ""),
            CreateTestIngestSettings(platform: ""),
            CreateTestIngestSettings(branch: ""),
            CreateTestIngestSettings(secret: ""),
        };

        foreach (var settings in invalidSettings)
        {
            Assert.False(settings.Validate().Successful);
        }
    }

    [Fact]
    public void TestsIngestSettings_Should_Reject_Payload_And_Payload_File_When_Both_Are_Supplied()
    {
        var settings = CreateTestIngestSettings(payloadFile: "payload.json");

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.NotNull(result.Message);
        Assert.Contains("exactly one", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestsIngestSettings_Should_Reject_Payload_File_When_File_Does_Not_Exist()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var settings = CreateTestIngestSettings(payload: null, payloadFile: missingPath);

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.NotNull(result.Message);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BadgeUpdateSettings_Should_Reject_Each_Required_Value_When_Value_Is_Empty()
    {
        var invalidSettings = new[]
        {
            CreateBadgeUpdateSettings(baseUrl: ""),
            CreateBadgeUpdateSettings(platform: ""),
            CreateBadgeUpdateSettings(commitSha: ""),
            CreateBadgeUpdateSettings(runId: ""),
            CreateBadgeUpdateSettings(repository: ""),
            CreateBadgeUpdateSettings(serverUrl: ""),
        };

        foreach (var settings in invalidSettings)
        {
            Assert.False(settings.Validate().Successful);
        }
    }

    private static BadgeUpdateSettings CreateBadgeUpdateSettings(
        string baseUrl = "https://api.example.com",
        string platform = "linux",
        string commitSha = "abc123",
        string runId = "42",
        string repository = "localstack-dotnet/badge-smith",
        string serverUrl = "https://github.com")
    {
        return new BadgeUpdateSettings
        {
            BaseUrl = baseUrl,
            Platform = platform,
            CommitSha = commitSha,
            RunId = runId,
            Repository = repository,
            ServerUrl = serverUrl,
        };
    }

    private static TestIngestSettings CreateTestIngestSettings(
        string baseUrl = "https://api.example.com",
        string owner = "localstack-dotnet",
        string repo = "badge-smith",
        string platform = "linux",
        string branch = "main",
        string secret = "test-secret",
        string? payload = "{}",
        string? payloadFile = null)
    {
        return new TestIngestSettings
        {
            BaseUrl = baseUrl,
            Owner = owner,
            Repo = repo,
            Platform = platform,
            Branch = branch,
            Secret = secret,
            Payload = payload,
            PayloadFile = payloadFile,
        };
    }
```

- [ ] **Step 5: Make secret mapping dry-run tests omit the table name**

In both tooling test files, remove these arguments from dry-run invocations:

```text
--table-name badge-smith-github-org-secrets
```

Keep the existing assertions that mapping content is validated, secret values are not printed, the command succeeds, and `ThrowingAwsClientFactory.Create` is never called.

- [ ] **Step 6: Run the new CLI tests and observe the current failures**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~BadgeSmithToolCommandTests|FullyQualifiedName~BadgeSmithToolInProcessTests"
```

Expected before implementation: badge update still requires `--hmac-secret`, tests-ingest accepts several invalid settings, and secret dry-run still requires a table name.

- [ ] **Step 7: Read badge HMAC from host configuration and complete badge validation**

In `BadgeUpdateCommand.cs`, add `using Microsoft.Extensions.Configuration;`, replace the unused logger field validation with configuration storage, and use this constructor:

```csharp
    private const string HmacSecretConfigurationKey = "BADGESMITH_HMAC_SECRET";

    private readonly IAnsiConsole _console;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public BadgeUpdateCommand(
        IHttpClientFactory httpClientFactory,
        IAnsiConsole console,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
```

Before payload construction or signing in `ExecuteAsync`, add:

```csharp
        var hmacSecret = _configuration[HmacSecretConfigurationKey];
        if (string.IsNullOrWhiteSpace(hmacSecret))
        {
            _console.MarkupLine($"[red]{HmacSecretConfigurationKey} is required.[/]");
            return ToolExitCodes.ValidationFailure;
        }
```

Change signature creation to:

```csharp
        var signature = HmacSigner.CreateSignature(payloadJson, hmacSecret);
```

Remove this property from `BadgeUpdateSettings` entirely:

```csharp
    [CommandOption("--hmac-secret")]
    [Description("HMAC secret for BadgeSmith authentication.")]
    public string HmacSecret { get; init; } = "";
```

Replace `BadgeUpdateSettings.Validate` with:

```csharp
    public override ValidationResult Validate()
    {
        if (!BadgeSmithUrlBuilder.TryCreate(BaseUrl, out _, out var baseUrlError))
        {
            return ValidationResult.Error(baseUrlError);
        }

        if (string.IsNullOrWhiteSpace(Platform))
        {
            return ValidationResult.Error("--platform is required.");
        }

        if (string.IsNullOrWhiteSpace(CommitSha))
        {
            return ValidationResult.Error("--commit-sha is required.");
        }

        if (string.IsNullOrWhiteSpace(RunId))
        {
            return ValidationResult.Error("--run-id is required.");
        }

        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            return ValidationResult.Error("--server-url is required.");
        }

        var repositoryParts = Repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (repositoryParts.Length != 2)
        {
            return ValidationResult.Error("--repository must be in owner/repo format.");
        }

        return ValidationResult.Success();
    }
```

- [ ] **Step 8: Complete tests-ingest validation before file or network I/O**

Replace `TestIngestSettings.Validate` with:

```csharp
    public override ValidationResult Validate()
    {
        if (!BadgeSmithUrlBuilder.TryCreate(BaseUrl, out _, out var baseUrlError))
        {
            return ValidationResult.Error(baseUrlError);
        }

        if (string.IsNullOrWhiteSpace(Owner))
        {
            return ValidationResult.Error("--owner is required.");
        }

        if (string.IsNullOrWhiteSpace(Repo))
        {
            return ValidationResult.Error("--repo is required.");
        }

        if (string.IsNullOrWhiteSpace(Platform))
        {
            return ValidationResult.Error("--platform is required.");
        }

        if (string.IsNullOrWhiteSpace(Branch))
        {
            return ValidationResult.Error("--branch is required.");
        }

        if (string.IsNullOrWhiteSpace(Secret))
        {
            return ValidationResult.Error("--secret is required.");
        }

        var hasPayloadFile = !string.IsNullOrWhiteSpace(PayloadFile);
        var hasPayload = !string.IsNullOrWhiteSpace(Payload);
        if (hasPayloadFile == hasPayload)
        {
            return ValidationResult.Error("Exactly one of --payload and --payload-file must be supplied.");
        }

        if (hasPayloadFile && !File.Exists(PayloadFile))
        {
            return ValidationResult.Error($"Payload file not found: {PayloadFile}");
        }

        return ValidationResult.Success();
    }
```

Spectre.Console.Cli calls this method before `ExecuteAsync`, so invalid settings cannot reach `File.ReadAllTextAsync`, HMAC signing, or HTTP client creation.

- [ ] **Step 9: Move dry-run before the secret table requirement**

In `SecretsSeedCommand.ExecuteAsync`, keep config existence validation first, resolve the optional table name, and replace the current table/dry-run block with:

```csharp
        if (settings.DryRun)
        {
            return await _seeder.SeedAsync(
                configPath,
                tableName ?? "",
                dynamoDb: null,
                secretsManager: null,
                dryRun: true,
                cts.Token).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            _console.MarkupLine("[red]Org secrets table name is required. Use --table-name or AWS_RESOURCE_ORG_SECRETS_TABLE.[/]");
            return ToolExitCodes.ValidationFailure;
        }

        var awsOptions = _awsOptionsResolver.Resolve(settings);
        using var awsClients = _awsClientFactory.Create(awsOptions);
        return await _seeder.SeedAsync(
            configPath,
            tableName,
            awsClients.DynamoDb,
            awsClients.SecretsManager,
            dryRun: false,
            cts.Token).ConfigureAwait(false);
```

The empty dry-run table string is never consumed because `OrgSecretSeeder.SeedAsync` returns from its dry-run branch before `PutMappingAsync`.

- [ ] **Step 10: Update the CLI example registered with Spectre**

Replace the badge example in `BadgeSmithTool.CreateCommandApp` with:

```csharp
                badge.AddCommand<BadgeUpdateCommand>("update")
                    .WithDescription("Post GitHub Actions test results to BadgeSmith.")
                    .WithExample("badge", "update", "--base-url", "https://badges.example.com", "--platform", "Linux", "--test-passed", "1", "--test-failed", "0", "--test-skipped", "0", "--repository", "localstack-dotnet/badge-smith", "--dry-run");
```

- [ ] **Step 11: Run focused CLI tests**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~BadgeSmithToolCommandTests|FullyQualifiedName~BadgeSmithToolInProcessTests"
```

Expected: all selected tests pass; badge update succeeds only with configured `BADGESMITH_HMAC_SECRET`, tests-ingest rejects invalid settings, and secret dry-run succeeds without a table or AWS client.

- [ ] **Step 12: Add static action contract tests before changing YAML**

Create `GitHubActionContractTests.cs` with:

```csharp
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Trait("Category", TestCategories.Unit)]
public sealed class GitHubActionContractTests
{
    [Fact]
    public void Update_Test_Badge_Action_Should_Resolve_Sdk_And_Tool_From_Action_Path()
    {
        var action = ReadRepositoryFile(".github", "workflows", "update-test-badge", "action.yml");

        Assert.Contains("uses: actions/setup-dotnet@v4", action, StringComparison.Ordinal);
        Assert.Contains("global-json-file: ${{ github.action_path }}/../../../global.json", action, StringComparison.Ordinal);
        Assert.Contains("$env:BADGESMITH_ACTION_PATH/../../../tools/badgesmith.cs", action, StringComparison.Ordinal);
        Assert.Contains("$BADGESMITH_ACTION_PATH/../../../tools/badgesmith.cs", action, StringComparison.Ordinal);
        Assert.DoesNotContain("github.workspace", action, StringComparison.Ordinal);
    }

    [Fact]
    public void Composite_Actions_Should_Not_Interpolate_Expressions_Inside_Run_Scripts()
    {
        var actionPaths = new[]
        {
            new[] { ".github", "workflows", "update-test-badge", "action.yml" },
            new[] { ".github", "workflows", "run-dotnet-tests", "action.yml" },
        };

        foreach (var actionPath in actionPaths)
        {
            var action = ReadRepositoryFile(actionPath);
            var scripts = ExtractRunScripts(action);
            Assert.NotEmpty(scripts);
            foreach (var script in scripts)
            {
                Assert.DoesNotContain("${{", script, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Run_Dotnet_Tests_Action_Should_Resolve_Tool_From_Workspace_Environment()
    {
        var action = ReadRepositoryFile(".github", "workflows", "run-dotnet-tests", "action.yml");

        Assert.Contains("BADGESMITH_TOOL_PATH: ${{ github.workspace }}/tools/badgesmith.cs", action, StringComparison.Ordinal);
        Assert.Contains("$env:BADGESMITH_TOOL_PATH", action, StringComparison.Ordinal);
        Assert.Contains("$BADGESMITH_TOOL_PATH", action, StringComparison.Ordinal);
    }

    [Fact]
    public void Ci_Workflow_Should_Use_File_System_Safe_Pr_Artifact_Name()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "ci-cd.yml");

        Assert.Contains("format('lambda-zip-pr-{0}', github.event.pull_request.number)", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("format('lambda-zip-{0}', github.head_ref || github.ref_name)", workflow, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }

    private static IReadOnlyList<string> ExtractRunScripts(string yaml)
    {
        var scripts = new List<string>();
        var currentScript = new List<string>();
        var runIndent = -1;

        foreach (var line in yaml.Split('\n'))
        {
            var trimmedLine = line.TrimStart();
            var indent = line.Length - trimmedLine.Length;
            if (trimmedLine.Equals("run: |", StringComparison.Ordinal))
            {
                runIndent = indent;
                currentScript.Clear();
                continue;
            }

            if (runIndent >= 0 && trimmedLine.Length > 0 && indent <= runIndent)
            {
                scripts.Add(string.Join('\n', currentScript));
                runIndent = -1;
            }

            if (runIndent >= 0)
            {
                currentScript.Add(line);
            }
        }

        if (runIndent >= 0)
        {
            scripts.Add(string.Join('\n', currentScript));
        }

        return scripts;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) && (File.Exists(gitPath) || Directory.Exists(gitPath)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find BadgeSmith repository root.");
    }
}
```

- [ ] **Step 13: Run action contract tests and observe the unsafe paths and interpolation**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~GitHubActionContractTests"
```

Expected before YAML changes: tests fail because the public action uses `github.workspace`, both actions interpolate expressions inside scripts, the public action does not install the pinned SDK, and the PR artifact name contains the slash-bearing branch name.

- [ ] **Step 14: Replace the public badge action with a remote-safe implementation**

Replace `.github/workflows/update-test-badge/action.yml` with:

```yaml
name: 'Update Test Results Badge'
description: 'Posts test results to a BadgeSmith API with HMAC authentication'
author: 'LocalStack .NET Team'

inputs:
  platform:
    description: 'Platform name (Linux, Windows, macOS)'
    required: true
  test_passed:
    description: 'Number of passed tests'
    required: true
  test_failed:
    description: 'Number of failed tests'
    required: true
  test_skipped:
    description: 'Number of skipped tests'
    required: true
  test_url_html:
    description: 'URL to test results page'
    required: false
    default: ''
  commit_sha:
    description: 'Git commit SHA'
    required: true
  run_id:
    description: 'GitHub Actions run ID'
    required: true
  repository:
    description: 'Repository in owner/repo format'
    required: true
  server_url:
    description: 'GitHub server URL'
    required: true
  api_base_url:
    description: 'Absolute BadgeSmith API base URL'
    required: true
  hmac_secret:
    description: 'HMAC secret for BadgeSmith authentication'
    required: true

runs:
  using: 'composite'
  steps:
    - name: 'Setup .NET'
      uses: actions/setup-dotnet@v4
      with:
        global-json-file: ${{ github.action_path }}/../../../global.json

    - name: 'Post Test Results to BadgeSmith API on Windows'
      if: runner.os == 'Windows'
      shell: pwsh
      env:
        BADGESMITH_ACTION_PATH: ${{ github.action_path }}
        BADGESMITH_HMAC_SECRET: ${{ inputs.hmac_secret }}
        BADGESMITH_PLATFORM: ${{ inputs.platform }}
        BADGESMITH_TEST_PASSED: ${{ inputs.test_passed }}
        BADGESMITH_TEST_FAILED: ${{ inputs.test_failed }}
        BADGESMITH_TEST_SKIPPED: ${{ inputs.test_skipped }}
        BADGESMITH_TEST_URL_HTML: ${{ inputs.test_url_html }}
        BADGESMITH_COMMIT_SHA: ${{ inputs.commit_sha }}
        BADGESMITH_RUN_ID: ${{ inputs.run_id }}
        BADGESMITH_REPOSITORY: ${{ inputs.repository }}
        BADGESMITH_SERVER_URL: ${{ inputs.server_url }}
        BADGESMITH_API_BASE_URL: ${{ inputs.api_base_url }}
        BADGESMITH_BRANCH: ${{ github.head_ref || github.ref_name }}
      run: |
        dotnet run --file "$env:BADGESMITH_ACTION_PATH/../../../tools/badgesmith.cs" -- badge update `
          --platform "$env:BADGESMITH_PLATFORM" `
          --test-passed "$env:BADGESMITH_TEST_PASSED" `
          --test-failed "$env:BADGESMITH_TEST_FAILED" `
          --test-skipped "$env:BADGESMITH_TEST_SKIPPED" `
          --test-url-html "$env:BADGESMITH_TEST_URL_HTML" `
          --commit-sha "$env:BADGESMITH_COMMIT_SHA" `
          --run-id "$env:BADGESMITH_RUN_ID" `
          --repository "$env:BADGESMITH_REPOSITORY" `
          --server-url "$env:BADGESMITH_SERVER_URL" `
          --base-url "$env:BADGESMITH_API_BASE_URL" `
          --branch "$env:BADGESMITH_BRANCH"

    - name: 'Post Test Results to BadgeSmith API on Unix'
      if: runner.os != 'Windows'
      shell: bash
      env:
        BADGESMITH_ACTION_PATH: ${{ github.action_path }}
        BADGESMITH_HMAC_SECRET: ${{ inputs.hmac_secret }}
        BADGESMITH_PLATFORM: ${{ inputs.platform }}
        BADGESMITH_TEST_PASSED: ${{ inputs.test_passed }}
        BADGESMITH_TEST_FAILED: ${{ inputs.test_failed }}
        BADGESMITH_TEST_SKIPPED: ${{ inputs.test_skipped }}
        BADGESMITH_TEST_URL_HTML: ${{ inputs.test_url_html }}
        BADGESMITH_COMMIT_SHA: ${{ inputs.commit_sha }}
        BADGESMITH_RUN_ID: ${{ inputs.run_id }}
        BADGESMITH_REPOSITORY: ${{ inputs.repository }}
        BADGESMITH_SERVER_URL: ${{ inputs.server_url }}
        BADGESMITH_API_BASE_URL: ${{ inputs.api_base_url }}
        BADGESMITH_BRANCH: ${{ github.head_ref || github.ref_name }}
      run: |
        "$BADGESMITH_ACTION_PATH/../../../tools/badgesmith.cs" badge update \
          --platform "$BADGESMITH_PLATFORM" \
          --test-passed "$BADGESMITH_TEST_PASSED" \
          --test-failed "$BADGESMITH_TEST_FAILED" \
          --test-skipped "$BADGESMITH_TEST_SKIPPED" \
          --test-url-html "$BADGESMITH_TEST_URL_HTML" \
          --commit-sha "$BADGESMITH_COMMIT_SHA" \
          --run-id "$BADGESMITH_RUN_ID" \
          --repository "$BADGESMITH_REPOSITORY" \
          --server-url "$BADGESMITH_SERVER_URL" \
          --base-url "$BADGESMITH_API_BASE_URL" \
          --branch "$BADGESMITH_BRANCH"
```

There is no separate display step; `BadgeUpdateCommand.WriteStepSummaryAsync` remains the only badge-markdown writer.

- [ ] **Step 15: Move internal test-action expressions into environment variables**

Replace `.github/workflows/run-dotnet-tests/action.yml` with:

```yaml
name: "Run .NET tests (multi-TFM)"
description: "Build once, test each target framework, unique TRX per framework"
inputs:
  project-path:
    description: "Path to the test .csproj file"
    required: true
  results-dir:
    description: "Directory for test result artefacts"
    required: true
  configuration:
    description: "Build configuration"
    default: "Release"
runs:
  using: "composite"
  steps:
    - if: runner.os == 'Windows'
      shell: pwsh
      env:
        BADGESMITH_TOOL_PATH: ${{ github.workspace }}/tools/badgesmith.cs
        BADGESMITH_PROJECT_PATH: ${{ inputs.project-path }}
        BADGESMITH_RESULTS_DIR: ${{ inputs.results-dir }}
        BADGESMITH_CONFIGURATION: ${{ inputs.configuration }}
      run: |
        dotnet run --file "$env:BADGESMITH_TOOL_PATH" -- tests run `
          --project-path "$env:BADGESMITH_PROJECT_PATH" `
          --results-dir "$env:BADGESMITH_RESULTS_DIR" `
          --configuration "$env:BADGESMITH_CONFIGURATION"

    - if: runner.os != 'Windows'
      shell: bash
      env:
        BADGESMITH_TOOL_PATH: ${{ github.workspace }}/tools/badgesmith.cs
        BADGESMITH_PROJECT_PATH: ${{ inputs.project-path }}
        BADGESMITH_RESULTS_DIR: ${{ inputs.results-dir }}
        BADGESMITH_CONFIGURATION: ${{ inputs.configuration }}
      run: |
        "$BADGESMITH_TOOL_PATH" tests run \
          --project-path "$BADGESMITH_PROJECT_PATH" \
          --results-dir "$BADGESMITH_RESULTS_DIR" \
          --configuration "$BADGESMITH_CONFIGURATION"
```

- [ ] **Step 16: Update CI consumption and install .NET in the ARM64 job**

In `.github/workflows/ci-cd.yml`, replace the badge action input:

```yaml
          api_base_url: 'https://api.localstackfor.net'
```

Remove the old `api_domain` input.

Replace the artifact name expression with a PR-number-based name that cannot contain
the `/` character rejected by `actions/upload-artifact`:

```yaml
          name: ${{ github.ref == 'refs/heads/master' && 'lambda-zip-latest' || format('lambda-zip-pr-{0}', github.event.pull_request.number) }}
```

Immediately after checkout in `continuous-deployment`, add:

```yaml
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
```

Keep the existing ARM64 Lambda build, artifact path, retention, and overwrite settings unchanged.

- [ ] **Step 17: Run action contract tests and actionlint**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~GitHubActionContractTests"
go run github.com/rhysd/actionlint/cmd/actionlint@v1.7.7 .github/workflows/ci-cd.yml .github/workflows/update-test-badge/action.yml .github/workflows/run-dotnet-tests/action.yml
```

Expected: action contract tests pass and actionlint exits `0` without diagnostics.

- [ ] **Step 18: Rewrite the action README around remote white-label use**

Replace `.github/workflows/update-test-badge/README.md` with:

````markdown
# Update Test Results Badge

Composite GitHub Action that posts CI test results to any BadgeSmith deployment
with HMAC authentication and writes badge markdown to the GitHub Actions step
summary.

## Inputs

See [`action.yml`](./action.yml) for the canonical input list. Required inputs are
`platform`, `test_passed`, `test_failed`, `test_skipped`, `commit_sha`, `run_id`,
`repository`, `server_url`, `api_base_url`, and `hmac_secret`.
`test_url_html` is optional. `api_base_url` must be an absolute HTTP or HTTPS URL
for the target deployment and may include a port or path prefix.

## Remote Usage

```yaml
- name: Update test badge
  uses: localstack-dotnet/badge-smith/.github/workflows/update-test-badge@v1
  with:
    platform: 'Linux'
    test_passed: '${{ steps.test-results.outputs.passed }}'
    test_failed: '${{ steps.test-results.outputs.failed }}'
    test_skipped: '${{ steps.test-results.outputs.skipped }}'
    commit_sha: '${{ github.sha }}'
    run_id: '${{ github.run_id }}'
    repository: '${{ github.repository }}'
    server_url: '${{ github.server_url }}'
    api_base_url: 'https://badges.example.com/api'
    hmac_secret: '${{ secrets.TESTDATASECRET }}'
```

For the LocalStack.NET deployment, set `api_base_url` to
`https://api.localstackfor.net` explicitly.

The action installs the SDK pinned by BadgeSmith's `global.json` and runs the
file-based CLI from the checked-out action repository. Callers do not copy the
tool into their repositories.

The `TESTDATASECRET` repository secret must hold the HMAC shared secret
configured for the organization through `badgesmith secrets seed`.
````

- [ ] **Step 19: Replace root action-copy guidance with the public remote action**

Replace `README.md` lines 151-174 with:

````markdown
## 🔄 **CI/CD Integration**

### **GitHub Actions**

Use the remotely reusable badge action and point it at your BadgeSmith deployment:

```yaml
- name: Update test badge
  uses: localstack-dotnet/badge-smith/.github/workflows/update-test-badge@v1
  with:
    platform: 'Linux'
    test_passed: '${{ steps.test-results.outputs.passed }}'
    test_failed: '${{ steps.test-results.outputs.failed }}'
    test_skipped: '${{ steps.test-results.outputs.skipped }}'
    commit_sha: '${{ github.sha }}'
    run_id: '${{ github.run_id }}'
    repository: '${{ github.repository }}'
    server_url: '${{ github.server_url }}'
    api_base_url: 'https://badges.example.com'
    hmac_secret: '${{ secrets.TESTDATASECRET }}'
```

Set `api_base_url` to `https://api.localstackfor.net` only when using the
LocalStack.NET deployment. The repository-local `run-dotnet-tests` action is an
internal BadgeSmith workflow helper, not a portable test-runner contract.
````

- [ ] **Step 20: Update CLI documentation for base URLs, environment HMAC, and dry-run**

In `tools/README.md`, ensure the inline tests-ingest dry-run includes:

```bash
  --base-url http://localhost:9474 \
```

Replace the badge update example with:

```bash
export BADGESMITH_HMAC_SECRET="$HMAC_SECRET"
./tools/badgesmith.cs badge update \
  --base-url https://badges.example.com/api \
  --platform Linux \
  --test-passed 190 --test-failed 0 --test-skipped 0 \
  --repository localstack-dotnet/badge-smith \
  --dry-run
```

Immediately after the example, document:

```markdown
`--base-url` is required and accepts an absolute HTTP or HTTPS deployment URL,
including a custom port or path prefix. `badge update` reads the HMAC secret from
`BADGESMITH_HMAC_SECRET`; it does not accept the secret as a command argument.
```

Replace the secret dry-run example with:

```bash
# Validate mapping content without a table name or AWS clients:
./tools/badgesmith.cs secrets seed --dry-run
```

Retain the table requirement in both non-dry-run LocalStack and real AWS examples.

- [ ] **Step 21: Run the complete focused tooling suite and file-based CLI checks**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~BadgeSmithToolCommandTests|FullyQualifiedName~BadgeSmithToolInProcessTests|FullyQualifiedName~BadgeSmithUrlBuilderTests|FullyQualifiedName~GitHubActionContractTests"
dotnet run --file tools/badgesmith.cs -- badge update --help
dotnet run --file tools/badgesmith.cs -- tests ingest --help
dotnet run --file tools/badgesmith.cs -- secrets seed --help
```

Expected: all focused tests pass; badge help contains `--base-url` but not `--api-domain` or `--hmac-secret`; tests-ingest help has no production URL default; secret help still documents table requirements for mutation.

- [ ] **Step 22: Scan active code and docs for removed contracts**

Run:

```bash
rg -n "api_domain|--api-domain|--hmac-secret" .github/workflows README.md tools --glob '!docs/superpowers/**'
```

Expected: no active action, CLI, test, or user documentation reference remains. The secret template phrase `your-hmac-secret-here` is unrelated and may remain.

- [ ] **Step 23: Inspect and commit the tooling/workflow change**

Run:

```bash
git diff --check
```

Expected: the diff contains only validation, environment-secret, action portability/security, ARM SDK setup, and matching documentation.

After presenting the required pre-commit summary and receiving approval, run:

```bash
git add tools/Commands/BadgeUpdateCommand.cs tools/Commands/TestIngestCommand.cs tools/Commands/SecretsSeedCommand.cs tools/Services/BadgeSmithTool.cs tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs tests/BadgeSmith.Api.Tests/Tooling/GitHubActionContractTests.cs .github/workflows/update-test-badge/action.yml .github/workflows/run-dotnet-tests/action.yml .github/workflows/ci-cd.yml .github/workflows/update-test-badge/README.md README.md tools/README.md
```

Expected: one focused commit matching the approved boundary.

## Plan Verification

- Badge HMAC data is absent from command arguments and generated shell text.
- Missing `BADGESMITH_HMAC_SECRET` returns `ToolExitCodes.ValidationFailure`.
- Tests ingest validates all required values, exactly one payload source, and file existence before I/O.
- Secret dry-run validates content without table configuration or AWS clients.
- The public action installs the pinned SDK and resolves its tool from `github.action_path` on Windows and Unix.
- The internal test action resolves its tool from `github.workspace` and interpolates no expressions inside scripts.
- `api_base_url` is required and white-label documentation names LocalStack.NET only as an explicit example.
- The ARM64 job installs the SDK declared by `global.json` before invoking the file-based CLI.
- The PR artifact name is `lambda-zip-pr-5`, so a slash-bearing feature branch cannot invalidate the upload.
- Action shell scripts contain no `${{ ... }}` interpolation.
