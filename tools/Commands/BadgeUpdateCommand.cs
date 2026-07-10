using BadgeSmith.Tools.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace BadgeSmith.Tools.Commands;

internal sealed class BadgeUpdateCommand : AsyncCommand<BadgeUpdateSettings>
{
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

    protected override async Task<int> ExecuteAsync(CommandContext context, BadgeUpdateSettings settings, CancellationToken cancellationToken)
    {
        var repositoryParts = settings.Repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var urls = BadgeSmithUrlBuilder.Create(settings.BaseUrl);

        var owner = repositoryParts[0].ToLowerInvariant();
        var repo = repositoryParts[1].ToLowerInvariant();
        var platform = settings.Platform.ToLowerInvariant();
        var branch = GitHubActions.ResolveBranch(settings.Branch);
        var total = settings.TestPassed + settings.TestFailed + settings.TestSkipped;
        var hmacSecret = _configuration[HmacSecretConfigurationKey];
        if (string.IsNullOrWhiteSpace(hmacSecret))
        {
            _console.MarkupLine($"[red]{HmacSecretConfigurationKey} is required.[/]");
            return ToolExitCodes.ValidationFailure;
        }

        var payload = new TestResultPayload(
            Platform: settings.Platform,
            Passed: settings.TestPassed,
            Failed: settings.TestFailed,
            Skipped: settings.TestSkipped,
            Total: total,
            UrlHtml: settings.TestUrlHtml ?? "",
            Timestamp: DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            Commit: settings.CommitSha,
            RunId: settings.RunId,
            WorkflowRunUrl: $"{settings.ServerUrl.TrimEnd('/')}/{settings.Repository}/actions/runs/{settings.RunId}");

        var payloadJson = JsonSerializer.Serialize(payload, ToolJsonSerializerContext.Default.TestResultPayload);
        var url = urls.BuildIngestUrl(platform, owner, repo, branch);
        var badgeUrl = urls.BuildBadgeUrl(platform, owner, repo, branch);
        var redirectUrl = urls.BuildRedirectUrl(platform, owner, repo, branch);
        var signature = HmacSigner.CreateSignature(payloadJson, hmacSecret);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");

        if (settings.DryRun)
        {
            _console.MarkupLine("[yellow]DRY RUN: request was not sent.[/]");
            await _console.Profile.Out.Writer.WriteLineAsync($"URL: {url}").ConfigureAwait(false);
            await _console.Profile.Out.Writer.WriteLineAsync($"Badge URL: {badgeUrl}").ConfigureAwait(false);
            await _console.Profile.Out.Writer.WriteLineAsync($"Redirect URL: {redirectUrl}").ConfigureAwait(false);
            await _console.Profile.Out.Writer.WriteLineAsync($"Payload: {payloadJson}").ConfigureAwait(false);
            await _console.Profile.Out.Writer.WriteLineAsync($"X-Timestamp: {timestamp}").ConfigureAwait(false);
            await _console.Profile.Out.Writer.WriteLineAsync($"X-Nonce: {nonce}").ConfigureAwait(false);
            await _console.Profile.Out.Writer.WriteLineAsync($"X-Signature: {signature}").ConfigureAwait(false);
            return ToolExitCodes.Success;
        }

        var client = _httpClientFactory.CreateClient("badgesmith-api");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url))
        {
            Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Nonce", nonce);
        request.Headers.Add("X-Signature", signature);

        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            _console.MarkupLine($"[green]Successfully posted to {Markup.Escape(url)} (HTTP {(int)response.StatusCode})[/]");
            await WriteStepSummaryAsync(badgeUrl, redirectUrl, settings.Platform, cancellationToken).ConfigureAwait(false);
            return ToolExitCodes.Success;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _console.MarkupLine($"[yellow]Failed to post to {Markup.Escape(url)} (HTTP {(int)response.StatusCode})[/]");
        await _console.Profile.Out.Writer.WriteLineAsync(errorBody).ConfigureAwait(false);

        if (settings.FailOnError)
        {
            return ToolExitCodes.NetworkFailure;
        }

        _console.MarkupLine("[yellow]Badge update failure does not fail CI by default. Use --fail-on-error to opt into non-zero exit.[/]");
        await WriteStepSummaryAsync(badgeUrl, redirectUrl, settings.Platform, cancellationToken).ConfigureAwait(false);
        return ToolExitCodes.Success;
    }

    private static async Task WriteStepSummaryAsync(string badgeUrl, string redirectUrl, string platformDisplay, CancellationToken cancellationToken)
    {
        var markdown = $"""
            **{platformDisplay} Badge:**
            [![Test Results ({platformDisplay})]({badgeUrl})]({redirectUrl})

            **Raw URLs:**
            - Badge: {badgeUrl}
            - Redirect: {redirectUrl}
            """;

        await GitHubActions.AppendStepSummaryAsync(markdown, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class BadgeUpdateSettings : CommandSettings
{
    [CommandOption("--platform")]
    [Description("Platform name (Linux, Windows, macOS).")]
    public string Platform { get; init; } = "";

    [CommandOption("--test-passed")]
    [Description("Number of passed tests.")]
    public int TestPassed { get; init; }

    [CommandOption("--test-failed")]
    [Description("Number of failed tests.")]
    public int TestFailed { get; init; }

    [CommandOption("--test-skipped")]
    [Description("Number of skipped tests.")]
    public int TestSkipped { get; init; }

    [CommandOption("--test-url-html")]
    [Description("URL to test results page.")]
    public string? TestUrlHtml { get; init; }

    [CommandOption("--commit-sha")]
    [Description("Git commit SHA.")]
    public string CommitSha { get; init; } = "";

    [CommandOption("--run-id")]
    [Description("GitHub Actions run ID.")]
    public string RunId { get; init; } = "";

    [CommandOption("--repository")]
    [Description("Repository in owner/repo format.")]
    public string Repository { get; init; } = "";

    [CommandOption("--server-url")]
    [Description("GitHub server URL.")]
    public string ServerUrl { get; init; } = "https://github.com";

    [CommandOption("--base-url")]
    [Description("BadgeSmith API base URL.")]
    public string BaseUrl { get; init; } = "";

    [CommandOption("--branch")]
    [Description("Git branch name (auto-detected from environment if not set).")]
    public string? Branch { get; init; }

    [CommandOption("--dry-run")]
    [Description("Print what would be sent without actually posting.")]
    public bool DryRun { get; init; }

    [CommandOption("--fail-on-error")]
    [Description("Exit with non-zero code when badge update fails.")]
    public bool FailOnError { get; init; }

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

        return Repository.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2
            ? ValidationResult.Success()
            : ValidationResult.Error("--repository must be in owner/repo format.");
    }
}
