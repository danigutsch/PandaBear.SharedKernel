namespace PandaBear.Repository.Tests;

/// <summary>The start information of a service process, as the CI tool requested it.</summary>
/// <param name="FileName">The executable that the tool asked to start, before the fake host replaced it with a script.</param>
/// <param name="Arguments">The arguments that the tool passed to the executable.</param>
/// <param name="WorkingDirectory">The directory in which the tool asked the process to run.</param>
/// <param name="RedirectsInput">Whether the tool redirected standard input, which it closes to ask the service to stop.</param>
/// <param name="RedirectsOutput">Whether the tool redirected standard output to a log file.</param>
/// <param name="RedirectsError">Whether the tool redirected standard error to a log file.</param>
/// <param name="UsesShell">Whether the tool asked the operating system shell to start the process.</param>
internal sealed record StartedService(
    string FileName,
    string[] Arguments,
    string WorkingDirectory,
    bool RedirectsInput,
    bool RedirectsOutput,
    bool RedirectsError,
    bool UsesShell);
