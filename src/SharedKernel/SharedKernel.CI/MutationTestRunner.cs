namespace PandaBear.CI;

/// <summary>
/// Runs each Stryker.NET config of <c>.config/sharedkernel-ci.json</c> in order, with the configured <c>dotnet-stryker</c>
/// installed on demand under the root, and stops at the first failure.
/// </summary>
public sealed class MutationTestRunner
{
    private readonly CiMachine _machine;

    /// <summary>Creates the runner from the machine that runs dotnet.</summary>
    /// <param name="machine">Runs the installation and Stryker.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/> is <see langword="null"/>.</exception>
    public MutationTestRunner(CiMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
    }

    /// <summary>Installs Stryker unless it is installed, then runs each config from its directory.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="output">Receives a line for each config and the output of each Stryker run.</param>
    /// <param name="ct">Cancels the running process.</param>
    /// <exception cref="InvalidOperationException">The configuration has no mutation runs, a config file is missing, or the installation or a run failed.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task Run(string root, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(output);
        var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
        var mutation = configuration.Mutation ?? throw CiConfigurationFile.Invalid("$.mutation is required by run-mutation-tests.");
        await MutationTestRun.Run(root, mutation, _machine.Run, output, ct).ConfigureAwait(false);
    }
}
