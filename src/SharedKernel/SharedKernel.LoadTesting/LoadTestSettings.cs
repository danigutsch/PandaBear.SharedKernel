namespace PandaBear.LoadTesting;

/// <summary>What one load-test run reads, selects, and allows.</summary>
public sealed class LoadTestSettings
{
    /// <summary>Gets the absolute repository root that the configuration, scripts, and results resolve under.</summary>
    public required string Root { get; init; }

    /// <summary>Gets the configuration file path relative to <see cref="Root"/>, such as <c>.config/sharedkernel-k6.json</c>.</summary>
    public required string ConfigurationPath { get; init; }

    /// <summary>Gets the name of the configured scenario to run.</summary>
    public required string Scenario { get; init; }

    /// <summary>Gets a value indicating whether the load generator runs in the configured container engine.</summary>
    public bool Container { get; init; }

    /// <summary>Gets the digest-pinned image that replaces the configured one in a container run, or <see langword="null"/>.</summary>
    public string? Image { get; init; }

    /// <summary>Gets a value indicating whether the target may be a host other than a loopback host.</summary>
    public bool AllowExternalTarget { get; init; }

    /// <summary>Gets a value indicating whether the arguments may turn on HTTP debugging, which can print request and response data.</summary>
    public bool AllowHttpDebug { get; init; }

    /// <summary>Gets a value indicating whether the arguments may send results, logs, or traces to custom outputs.</summary>
    public bool AllowRemoteOutput { get; init; }

    /// <summary>Gets the arguments that reach the load generator before the script.</summary>
    public IReadOnlyList<string> GeneratorArguments { get; init; } = [];

    /// <summary>
    /// Gets the environment that the run reads: the target base URL without a host, the scenario variables, and the
    /// variables a local load generator keeps. The host reads it; the runner never reads the process environment.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
