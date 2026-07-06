using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using Spectre.Console;

namespace BadgeSmith.Tools.Infrastructure;

internal sealed class ProcessRunner
{
    private readonly IAnsiConsole _console;
    private readonly bool _verbose;

    public ProcessRunner(IAnsiConsole console, bool verbose)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _verbose = verbose;
    }

    public async Task<BufferedProcessResult> RunBufferedAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        CancellationToken cancellationToken = default)
    {
        var command = CreateCommand(executable, arguments, workingDirectory, environment, allowNonZeroExit);
        WriteCommand(executable, arguments, workingDirectory);

        var result = await command.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        if (_verbose && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            _console.WriteLine(result.StandardOutput.TrimEnd());
        }

        if (_verbose && !string.IsNullOrWhiteSpace(result.StandardError))
        {
            _console.MarkupLine($"[yellow]{Markup.Escape(result.StandardError.TrimEnd())}[/]");
        }

        return new BufferedProcessResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    public async Task<int> RunStreamingAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        CancellationToken cancellationToken = default)
    {
        var command = CreateCommand(executable, arguments, workingDirectory, environment, allowNonZeroExit);
        WriteCommand(executable, arguments, workingDirectory);

        await foreach (var commandEvent in command.ListenAsync(cancellationToken).ConfigureAwait(false))
        {
            switch (commandEvent)
            {
                case StandardOutputCommandEvent output:
                    _console.WriteLine(output.Text);
                    break;
                case StandardErrorCommandEvent error:
                    _console.WriteLine(error.Text);
                    break;
                case ExitedCommandEvent exited:
                    return exited.ExitCode;
            }
        }

        return ToolExitCodes.Success;
    }

    private static Command CreateCommand(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string?>? environment,
        bool allowNonZeroExit)
    {
        var command = Cli.Wrap(executable)
            .WithArguments(arguments);

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            command = command.WithWorkingDirectory(workingDirectory);
        }

        if (environment is not null)
        {
            command = command.WithEnvironmentVariables(environment);
        }

        if (allowNonZeroExit)
        {
            command = command.WithValidation(CommandResultValidation.None);
        }

        return command;
    }

    private void WriteCommand(string executable, IReadOnlyList<string> arguments, string? workingDirectory)
    {
        if (!_verbose)
        {
            return;
        }

        var directory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory;
        _console.MarkupLine($"[grey]> ({Markup.Escape(directory)}) {Markup.Escape(executable)} {Markup.Escape(string.Join(' ', arguments))}[/]");
    }
}

internal readonly record struct BufferedProcessResult(int ExitCode, string StandardOutput, string StandardError);
