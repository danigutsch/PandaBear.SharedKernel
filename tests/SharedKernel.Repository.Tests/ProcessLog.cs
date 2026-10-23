namespace PandaBear.Repository.Tests;

/// <summary>Records child process requests and answers them without starting processes.</summary>
internal sealed class ProcessLog(Func<string, string[], CancellationToken, Task<string>> respond)
{
    /// <summary>Answers <c>git rev-parse</c> with <c>fixture</c> and every other request with no output.</summary>
    public ProcessLog()
        : this(static (fileName, arguments, _) => Task.FromResult(fileName == "git" && arguments is ["rev-parse", ..] ? "fixture" : string.Empty))
    {
    }

    public List<ProcessCall> Calls { get; } = [];

    public string[] Commands => [.. Calls.Select(static call => call.FileName + " " + string.Join(' ', call.Arguments))];

    public Task<string> Run(string fileName, string[] arguments, string workingDirectory, CancellationToken ct)
    {
        Calls.Add(new ProcessCall(fileName, arguments, workingDirectory, ct));
        return respond(fileName, arguments, ct);
    }
}
