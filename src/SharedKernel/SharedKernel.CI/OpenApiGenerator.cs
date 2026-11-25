namespace SharedKernel.CI;

/// <summary>
/// Builds the OpenAPI targets of <c>.config/sharedkernel-ci.json</c> with their generate or refresh property, through the
/// dotnet host that runs the caller and with an environment that keeps only what the build needs.
/// </summary>
public sealed class OpenApiGenerator
{
    private readonly CiMachine _machine;

    /// <summary>Creates the generator from the machine that runs the builds.</summary>
    /// <param name="machine">Runs the builds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/> is <see langword="null"/>.</exception>
    public OpenApiGenerator(CiMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
    }

    /// <summary>Builds each named target in order, or every configured target when <paramref name="targets"/> is empty.</summary>
    /// <param name="root">The repository root, the working directory of each build.</param>
    /// <param name="targets">The target names.</param>
    /// <param name="refresh">Whether to set the refresh property, which also updates the committed copies, instead of the generate property.</param>
    /// <param name="error">Receives each build's output.</param>
    /// <param name="ct">Cancels the builds.</param>
    /// <exception cref="ArgumentException">A name is not a configured target.</exception>
    /// <exception cref="InvalidOperationException">The configuration is not valid or defines no target, or a build failed.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task Generate(string root, IReadOnlyCollection<string> targets, bool refresh, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(error);
        var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
        await OpenApiGeneration.Run(root, configuration, targets, refresh, null, _machine.RunConfigured, error, ct).ConfigureAwait(false);
    }
}
