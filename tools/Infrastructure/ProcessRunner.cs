using BadgeSmith.Tools.Services;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using Spectre.Console;

namespace BadgeSmith.Tools.Infrastructure;

internal interface IProcessRunner
{
    public Task<BufferedProcessResult> RunBufferedAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        bool verbose = false,
        CancellationToken cancellationToken = default);

    public Task<int> RunStreamingAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        bool verbose = false,
        CancellationToken cancellationToken = default);
}

internal sealed class ProcessRunner : IProcessRunner
{
    private readonly IAnsiConsole _console;
    private readonly IToolLogger _logger;

    public ProcessRunner(IAnsiConsole console, IToolLogger logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BufferedProcessResult> RunBufferedAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        var command = CreateCommand(executable, arguments, workingDirectory, environment, allowNonZeroExit);
        WriteCommand(executable, arguments, workingDirectory, verbose);

        var result = await command.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        if (verbose && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            _logger.Info(result.StandardOutput.TrimEnd());
        }

        if (verbose && !string.IsNullOrWhiteSpace(result.StandardError))
        {
            _logger.Warning(result.StandardError.TrimEnd());
        }

        return new BufferedProcessResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    public async Task<int> RunStreamingAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        var command = CreateCommand(executable, arguments, workingDirectory, environment, allowNonZeroExit);
        WriteCommand(executable, arguments, workingDirectory, verbose);

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

    private void WriteCommand(string executable, IReadOnlyList<string> arguments, string? workingDirectory, bool verbose)
    {
        if (!verbose)
        {
            return;
        }

        var directory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory;
        _logger.Debug($"> ({directory}) {executable} {string.Join(' ', arguments)}");
    }
}

internal readonly record struct BufferedProcessResult(int ExitCode, string StandardOutput, string StandardError);
