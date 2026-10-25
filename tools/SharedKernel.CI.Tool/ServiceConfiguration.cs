namespace PandaBear.CI.Tool;

/// <summary>
/// A process that a slice starts once before its tests run and stops after them, such as one database server that the
/// slice's test projects share. The process writes a value to a ready file when it serves, and exits when its standard
/// input reaches end-of-file.
/// </summary>
/// <param name="Project">The repository-relative solution project that <c>dotnet run</c> starts; it is not a test project.</param>
/// <param name="ReadyFileVariable">The environment variable, in the service's environment, that names the file where the service writes its value.</param>
/// <param name="ValueVariable">The environment variable, in the environment of the slice's test run only, that carries the value the service wrote.</param>
/// <param name="ReadyTimeoutSeconds">How long the service has to write its value after it starts.</param>
/// <param name="StopTimeoutSeconds">How long the service has to exit after its standard input closes before its process tree is killed.</param>
internal sealed record ServiceConfiguration(
    string Project,
    string ReadyFileVariable,
    string ValueVariable,
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
