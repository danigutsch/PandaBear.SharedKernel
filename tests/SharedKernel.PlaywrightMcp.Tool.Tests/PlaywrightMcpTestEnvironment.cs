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
        return PlaywrightMcpCommandLine.Create(GetEnvironmentVariable, ResolveExecutable, runProcess)
            .Parse(args)
            .InvokeAsync(configuration, ct);
    }
}
