namespace PandaBear.LoadTesting.Tests;

/// <summary>A load generator that records the run it receives and returns a fixed exit code.</summary>
/// <param name="exitCode">The exit code that <see cref="Run"/> returns.</param>
internal sealed class RecordingLoadGenerator(int exitCode = 0) : ILoadGenerator
{
    /// <summary>The variable that the generator controls.</summary>
    public const string ControlledVariable = "GEN_CONTROLLED";

    /// <summary>Gets the run that <see cref="Run"/> received, or <see langword="null"/> when it did not run.</summary>
    public LoadGeneratorRun? Received { get; private set; }

    /// <summary>Gets the controlled variables that <see cref="ValidateArguments"/> received.</summary>
    public IReadOnlyCollection<string>? CheckedVariables { get; private set; }

    /// <summary>Gets or sets the argument that <see cref="ValidateArguments"/> rejects.</summary>
    public string? RejectedArgument { get; set; }

    /// <inheritdoc />
    public string Command => "gen";

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string>> ControlledVariables { get; } = [new(ControlledVariable, "on")];

    /// <inheritdoc />
    public void ValidateArguments(IReadOnlyList<string> arguments, IReadOnlyCollection<string> controlledVariables, LoadTestSettings settings)
    {
        CheckedVariables = controlledVariables;
        if (RejectedArgument is not null && arguments.Contains(RejectedArgument))
        {
            throw new InvalidOperationException("rejected " + RejectedArgument);
        }
    }

    /// <inheritdoc />
    public Task<int> Run(LoadGeneratorRun run, CancellationToken ct)
    {
        Received = run;
        return Task.FromResult(exitCode);
    }
}
