using BadgeSmith.Tools.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;

namespace BadgeSmith.Tools.Commands;

internal sealed class TestIngestCommand : AsyncCommand<TestIngestSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, TestIngestSettings settings, CancellationToken cancellationToken)
    {
        var payloadJson = settings.PayloadFile is { Length: > 0 }
            ? await File.ReadAllTextAsync(settings.PayloadFile, cancellationToken).ConfigureAwait(false)
            : settings.Payload ?? string.Empty;

        var owner = settings.Owner.ToLowerInvariant();
        var repo = settings.Repo.ToLowerInvariant();
        var platform = settings.Platform.ToLowerInvariant();
        var branch = settings.Branch;
        var url = $"{settings.BaseUrl.TrimEnd('/')}/tests/results/{platform}/{owner}/{repo}/{branch}";
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");
        var signature = HmacSigner.CreateSignature(payloadJson, settings.Secret);

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]DRY RUN: request was not sent.[/]");
            await Console.Out.WriteLineAsync(url).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"X-Timestamp: {timestamp}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"X-Nonce: {nonce}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"X-Signature: {signature}").ConfigureAwait(false);
            return ToolExitCodes.Success;
        }

        using var client = new HttpClient();
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
            AnsiConsole.MarkupLine($"[green]Successfully posted to {Markup.Escape(url)} (HTTP {(int)response.StatusCode})[/]");
            if (settings.Verbose)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                AnsiConsole.WriteLine(responseBody);
            }

            return ToolExitCodes.Success;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"[red]Failed to post to {Markup.Escape(url)} (HTTP {(int)response.StatusCode})[/]");
        AnsiConsole.WriteLine(errorBody);
        return ToolExitCodes.NetworkFailure;
    }
}

internal sealed class TestIngestSettings : CommandSettings
{
    [CommandOption("--base-url")]
    [Description("BadgeSmith API base URL.")]
    public string BaseUrl { get; init; } = "https://api.localstackfor.net";

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
        var hasPayloadFile = PayloadFile is { Length: > 0 };
        var hasPayload = Payload is { Length: > 0 };
        if (!hasPayloadFile && !hasPayload)
        {
            return ValidationResult.Error("Either --payload or --payload-file must be supplied.");
        }

        return ValidationResult.Success();
    }
}
