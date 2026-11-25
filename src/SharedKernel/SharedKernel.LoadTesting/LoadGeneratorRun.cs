namespace SharedKernel.LoadTesting;

/// <summary>A prepared run that <see cref="LoadTestRunner"/> hands to an <see cref="ILoadGenerator"/>.</summary>
public sealed class LoadGeneratorRun
{
    /// <summary>Gets the absolute path of the local generator, or of the container engine for a container run.</summary>
    public required string Executable { get; init; }

    /// <summary>Gets the absolute repository root, which is the working directory of the generator.</summary>
    public required string Root { get; init; }

    /// <summary>Gets the absolute script directory, which a container run mounts read-only.</summary>
    public required string ScriptDirectory { get; init; }

    /// <summary>Gets the script path relative to <see cref="ScriptDirectory"/>, with <c>/</c> separators.</summary>
    public required string Script { get; init; }

    /// <summary>Gets the absolute path of the script, which exists.</summary>
    public required string ScriptPath { get; init; }

    /// <summary>Gets the absolute directory that this run created for its results.</summary>
    public required string RunDirectory { get; init; }

    /// <summary>
    /// Gets the variables, in order, that reach the script: the base URL, <see cref="ILoadGenerator.ControlledVariables"/>,
    /// and each set scenario variable. A container run's base URL already reaches loopback hosts through
    /// <see cref="LoadTestContainer.HostAlias"/>.
    /// </summary>
    public required IReadOnlyList<KeyValuePair<string, string>> Variables { get; init; }

    /// <summary>Gets the validated arguments that reach the generator before the script.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Gets the container to run the generator in, or <see langword="null"/> for a local run.</summary>
    public LoadTestContainer? Container { get; init; }

    /// <summary>Gets the environment of the run, from which a local generator keeps only what the platform needs.</summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>Gets the writer that receives the generator's diagnostics, such as a container left running.</summary>
    public required TextWriter Error { get; init; }
}
