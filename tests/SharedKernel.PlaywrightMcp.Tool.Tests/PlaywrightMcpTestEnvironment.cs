using System.CommandLine;
using System.Diagnostics;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

internal sealed class PlaywrightMcpTestEnvironment
{
    private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _executables = new(StringComparer.Ordinal);

    public StringWriter Output { get; } = new();

    public StringWriter Error { get; } = new();

    public string? GetEnvironmentVariable(string name)
    {
        return _environment.GetValueOrDefault(name);
    }

    public static string GetExecutablePath(string name)
    {
        var fileName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "playwright-mcp-tests", fileName));
    }

    public string? ResolveExecutable(string name)
    {
        return _executables.GetValueOrDefault(name);
    }

    public void SetEnvironmentVariable(string name, string? value)
    {
        _environment[name] = value;
    }

    public void SetExecutable(string name, string? path)
    {
        _executables[name] = path;
    }

    public Task<int> Invoke(
        string[] args,
        Func<ProcessStartInfo, CancellationToken, Task<ProcessResult>> runProcess,
        CancellationToken ct)
    {
        var configuration = new InvocationConfiguration
        {
            Output = Output,
            Error = Error,
            // Signal handlers are process-wide; PlaywrightMcpTerminationTests cover them in a separate process.
            ProcessTerminationTimeout = null
        };
        return PlaywrightMcpCommandLine.Create(
                GetEnvironmentVariable,
                ResolveExecutable,
                (startInfo, forward, token) => Forward(runProcess(startInfo, token), forward))
            .Parse(args)
            .InvokeAsync(configuration, ct);
    }

    // A double returns the engine output; when the run forwards it, it reaches the writer and leaves the result empty, as
    // the process runner's forwarded lines do.
    private static async Task<ProcessResult> Forward(Task<ProcessResult> run, TextWriter? forward)
    {
        var result = await run;
        if (forward is null)
        {
            return result;
        }

        await forward.WriteAsync(result.StandardOutput);
        await forward.WriteAsync(result.StandardError);
        return result with
        {
            StandardOutput = string.Empty,
            StandardError = string.Empty
        };
    }
}
