namespace PandaBear.K6.Tool;

/// <summary>A k6 scenario of <c>.config/sharedkernel-k6.json</c>.</summary>
/// <param name="Name">The name that <c>run --scenario</c> selects.</param>
/// <param name="Script">The script path, relative to the script directory.</param>
/// <param name="BaseUrlVariable">
/// The k6 variable that receives the target base URL. Without a host, the tool reads the URL from the environment
/// variable of the same name.
/// </param>
/// <param name="Environment">Environment variables that reach k6 as <c>-e NAME=value</c> when they are set.</param>
/// <param name="Host">The process that serves the target for the duration of the run.</param>
internal sealed record K6ScenarioConfiguration(
    string Name,
    string Script,
    string BaseUrlVariable,
    string[]? Environment = null,
    K6HostConfiguration? Host = null);
