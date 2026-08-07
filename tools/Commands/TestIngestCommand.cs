using BadgeSmith.Tools.Infrastructure;
using BadgeSmith.Tools.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;

namespace BadgeSmith.Tools.Commands;

internal sealed class TestIngestCommand : AsyncCommand<TestIngestSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IHttpClientFactory _httpClientFactory;

    public TestIngestCommand(IHttpClientFactory httpClientFactory, IAnsiConsole console, IToolLogger logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        ArgumentNullException.ThrowIfNull(logger);
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, TestIngestSettings settings, CancellationToken cancellationToken)
    {
        var urls = BadgeSmithUrlBuilder.Create(settings.BaseUrl);

        var payloadJson = settings.PayloadFile is { Length: > 0 }
            ? await File.ReadAllTextAsync(settings.PayloadFile, cancellationToken).ConfigureAwait(false)
            : settings.Payload ?? string.Empty;

        var owner = settings.Owner.ToLowerInvariant();
        var repo = settings.Repo.ToLowerInvariant();
        var platform = settings.Platform.ToLowerInvariant();
        var branch = settings.Branch;
        var url = urls.BuildIngestUrl(platform, owner, repo, branch);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");
        var signature = HmacSigner.CreateSignature(owner, repo, platform, branch, timestamp, nonce, payloadJson, settings.Secret);

        if (settings.DryRun)
        {
            _console.MarkupLine("[yellow]DRY RUN: request was not sent.[/]");
            await _console.Profile.Out.Writer.WriteLineAsync(url).ConfigureAwait(false);
            await _console.Profile.Out.Writer.WriteLineAsync($"Payload: {payloadJson}").ConfigureAwait(false);
            await _console.Profile.Out.Writer.WriteLineAsync($"X-Timestamp: {timestamp}").ConfigureAwait(false);
            await _console.Profile.Out.Writer.WriteLineAsync($"X-Nonce: {nonce}").ConfigureAwait(false);
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
            if (settings.Verbose)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                await _console.Profile.Out.Writer.WriteLineAsync(responseBody).ConfigureAwait(false);
            }

            return ToolExitCodes.Success;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _console.MarkupLine($"[red]Failed to post to {Markup.Escape(url)} (HTTP {(int)response.StatusCode})[/]");
        await _console.Profile.Out.Writer.WriteLineAsync(errorBody).ConfigureAwait(false);
        return ToolExitCodes.NetworkFailure;
    }
}

internal sealed class TestIngestSettings : CommandSettings
{
    [CommandOption("--base-url")]
    [Description("BadgeSmith API base URL.")]
    public string BaseUrl { get; init; } = "";

    [CommandOption("--owner")]
    [Description("GitHub repository owner.")]
    public string Owner { get; init; } = "";

    [CommandOption("--repo")]
    [Description("GitHub repository name.")]
    public string Repo { get; init; } = "";

    [CommandOption("--platform")]
    [Description("Test platform (Linux, Windows, macOS).")]
    public string Platform { get; init; } = "";

    [CommandOption("--branch")]
    [Description("Git branch name.")]
    public string Branch { get; init; } = "unknown";

    [CommandOption("--secret")]
    [Description("HMAC shared secret.")]
    public string Secret { get; init; } = "";

    [CommandOption("--payload-file")]
    [Description("Path to a JSON payload file.")]
    public string? PayloadFile { get; init; }

    [CommandOption("--payload")]
    [Description("Inline JSON payload string.")]
    public string? Payload { get; init; }

    [CommandOption("--dry-run")]
    [Description("Print what would be sent without actually posting.")]
    public bool DryRun { get; init; }

    [CommandOption("-v|--verbose")]
    [Description("Print verbose output.")]
    public bool Verbose { get; init; }

    public override ValidationResult Validate()
    {
        if (!BadgeSmithUrlBuilder.TryCreate(BaseUrl, out var urls, out var baseUrlError))
        {
            return ValidationResult.Error(baseUrlError);
        }

        if (!urls.TryValidateSecureTransport(out var transportError))
        {
            return ValidationResult.Error(transportError);
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
}
