namespace PandaBear.Repository.Tests;

/// <summary>The start information of a service process, as the CI tool requested it.</summary>
internal sealed record StartedService(
    string FileName,
    string[] Arguments,
    string WorkingDirectory,
    bool RedirectsInput,
    bool RedirectsOutput,
    bool RedirectsError,
    bool UsesShell);
