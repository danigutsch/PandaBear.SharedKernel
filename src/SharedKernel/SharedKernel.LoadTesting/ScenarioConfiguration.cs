namespace PandaBear.LoadTesting;

/// <summary>A scenario of the load-test configuration file.</summary>
/// <param name="Name">The name that <see cref="LoadTestSettings.Scenario"/> selects.</param>
/// <param name="Script">The script path, relative to the script directory.</param>
/// <param name="BaseUrlVariable">
/// The script variable that receives the target base URL. Without a host, the runner reads the URL from the environment
/// variable of the same name.
/// </param>
/// <param name="Environment">Environment variables that reach the script when they are set.</param>
/// <param name="Host">The process that serves the target for the duration of the run.</param>
internal sealed record ScenarioConfiguration(
    string Name,
    string Script,
    string BaseUrlVariable,
    string[]? Environment = null,
    HostConfiguration? Host = null);
