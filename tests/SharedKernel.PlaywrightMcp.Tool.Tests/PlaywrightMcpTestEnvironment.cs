using System.CommandLine;
using System.Diagnostics;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

/// <summary>A fake environment, executable lookup, and output writers for invoking the Playwright MCP command line without starting real processes.</summary>
internal sealed class PlaywrightMcpTestEnvironment
{
    private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _executables = new(StringComparer.Ordinal);

    /// <summary>Captures standard output.</summary>
    public StringWriter Output { get; } = new();

    /// <summary>Captures standard error.</summary>
    public StringWriter Error { get; } = new();

    /// <summary>Returns a variable set with <c>SetEnvironmentVariable</c>, or <c>null</c>.</summary>
    public string? GetEnvironmentVariable(string name)
    {
        return _environment.GetValueOrDefault(name);
    }

    /// <summary>Returns an absolute path under the temporary directory for a fake executable, with <c>.exe</c> on Windows.</summary>
    public static string GetExecutablePath(string name)
    {
        var fileName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "playwright-mcp-tests", fileName));
    }

    /// <summary>Returns the path set for an executable name with <c>SetExecutable</c>, or <c>null</c>.</summary>
    public string? ResolveExecutable(string name)
    {
        return _executables.GetValueOrDefault(name);
    }

    /// <summary>Sets, or with <c>null</c> clears, a variable the command line sees.</summary>
    public void SetEnvironmentVariable(string name, string? value)
    {
        _environment[name] = value;
    }

    /// <summary>Sets, or with <c>null</c> clears, the path an executable name resolves to.</summary>
    public void SetExecutable(string name, string? path)
    {
        _executables[name] = path;
    }

    /// <summary>Parses and runs the command line, handing each process start to <c>runProcess</c> instead of starting it, and returns the exit code.</summary>
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
