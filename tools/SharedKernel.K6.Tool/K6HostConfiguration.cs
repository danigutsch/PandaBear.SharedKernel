namespace PandaBear.K6.Tool;

/// <summary>A process that the tool starts before k6, waits for, and stops after k6.</summary>
/// <param name="Command">The command name, resolved from the absolute directories of <c>PATH</c>.</param>
/// <param name="Arguments">The arguments; an argument <c>{listenUrl}</c> becomes the address the host binds.</param>
/// <param name="ReadyFileVariable">The environment variable that names the file where the host writes its base URL.</param>
/// <param name="HealthPath">The path, starting with <c>/</c>, that answers a success status once the host is ready.</param>
/// <param name="ReadyTimeoutSeconds">How long the host has to write its base URL.</param>
/// <param name="HealthTimeoutSeconds">How long the host has to answer the health path after writing its base URL.</param>
internal sealed record K6HostConfiguration(
    string Command,
    string[] Arguments,
    string ReadyFileVariable,
    string HealthPath,
    int ReadyTimeoutSeconds,
    int HealthTimeoutSeconds);
