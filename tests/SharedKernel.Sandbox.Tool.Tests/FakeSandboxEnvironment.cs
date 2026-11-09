namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>A scripted environment: every operation answers from a table and every call is recorded.</summary>
internal sealed class FakeSandboxEnvironment : ISandboxEnvironment
{
    private readonly List<string> _calls = [];

    /// <summary>Gets or sets the answer for creating a Unix socket.</summary>
    public (ProbeOutcome, string) Socket { get; set; } = (ProbeOutcome.Allowed, "socket ok");

    /// <summary>Gets or sets the answer for creating a named pipe.</summary>
    public (ProbeOutcome, string) Pipe { get; set; } = (ProbeOutcome.Allowed, "pipe ok");

    /// <summary>Gets the answers for writing, by directory; a directory without an entry is allowed.</summary>
    public Dictionary<string, (ProbeOutcome, string)> Writes { get; } = [];

    /// <summary>Gets the answers for reading, by path; a path without an entry is skipped.</summary>
    public Dictionary<string, (ProbeOutcome, string)> Reads { get; } = [];

    /// <inheritdoc />
    public string HomeDirectory { get; set; } = "/home/user";

    /// <inheritdoc />
    public string? AccountHomeDirectory
    {
        get; set;
    }

    /// <inheritdoc />
    public string TempDirectory => "/tmp/x";

    /// <inheritdoc />
    public string? NuGetPackagesDirectory { get; set; } = "/home/user/.nuget/packages";

    /// <summary>Gets what was tried, in order.</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <inheritdoc />
    public (ProbeOutcome Outcome, string Detail) CreateUnixSocket()
    {
        _calls.Add("socket");
        return Socket;
    }

    /// <inheritdoc />
    public (ProbeOutcome Outcome, string Detail) CreateNamedPipe()
    {
        _calls.Add("pipe");
        return Pipe;
    }

    /// <inheritdoc />
    public (ProbeOutcome Outcome, string Detail) WriteFile(string directory)
    {
        _calls.Add("write " + directory);
        return Writes.TryGetValue(directory, out var answer) ? answer : (ProbeOutcome.Allowed, "write ok");
    }

    /// <inheritdoc />
    public (ProbeOutcome Outcome, string Detail) ReadPath(string path)
    {
        _calls.Add("read " + path);
        return Reads.TryGetValue(path, out var answer) ? answer : (ProbeOutcome.Skipped, "nothing");
    }
}
