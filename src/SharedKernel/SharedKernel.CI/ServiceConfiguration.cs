namespace PandaBear.CI;

/// <summary>
/// A process that a slice starts once before its tests run and stops after them, such as the servers that the slice's test
/// projects share. The process writes one <c>NAME=value</c> line for each of its value variables to a ready file when it
/// serves, and exits when its standard input reaches end-of-file.
/// </summary>
/// <param name="Project">The repository-relative solution project that <c>dotnet run</c> starts; it is not a test project.</param>
/// <param name="ReadyFileVariable">The environment variable, in the service's environment, that names the file where the service writes its values.</param>
/// <param name="ValueVariables">
/// The environment variables, in the environment of the slice's test run only, that carry the values the service wrote: at least one,
/// each the <c>NAME</c> of one line of the ready file.
/// </param>
/// <param name="ReadyTimeoutSeconds">How long the service has to write its values after it starts.</param>
/// <param name="StopTimeoutSeconds">How long the service has to exit after its standard input closes before its process tree is killed.</param>
public sealed record ServiceConfiguration(
    string Project,
    string ReadyFileVariable,
    IReadOnlyList<string> ValueVariables,
    int ReadyTimeoutSeconds = ServiceConfiguration.DefaultReadyTimeoutSeconds,
    int StopTimeoutSeconds = ServiceConfiguration.DefaultStopTimeoutSeconds)
{
    /// <summary>The default time to become ready: enough for a container image pull on a cold runner.</summary>
    public const int DefaultReadyTimeoutSeconds = 300;

    /// <summary>The default time to exit gracefully: enough to stop and remove containers.</summary>
    public const int DefaultStopTimeoutSeconds = 60;

    /// <summary>The largest accepted timeout.</summary>
    public const int MaxTimeoutSeconds = 3600;
}
