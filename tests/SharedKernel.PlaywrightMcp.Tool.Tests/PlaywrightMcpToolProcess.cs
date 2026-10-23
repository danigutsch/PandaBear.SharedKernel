using System.Diagnostics;
using System.Globalization;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

internal sealed class PlaywrightMcpToolProcess : IAsyncDisposable
{
    private static readonly string[] IsolatedVariables =
    [
        "PLAYWRIGHT_MCP_ENGINE",
        "PLAYWRIGHT_MCP_NETWORK_ACCESS",
        "PLAYWRIGHT_MCP_IMAGE_RESPONSES",
        "DOCKER_HOST",
        "DOCKER_CONTEXT",
        "CONTAINER_HOST",
        "CONTAINER_CONNECTION"
    ];

    private readonly Process _process;
    private readonly Task<string> _standardOutput;
    private readonly Task<string> _standardError;

    private PlaywrightMcpToolProcess(Process process)
    {
        _process = process;
        _standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        _standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);
    }

    public static PlaywrightMcpToolProcess StartServer(string path)
    {
        var fileName = OperatingSystem.IsWindows()
            ? "SharedKernel.PlaywrightMcp.Tool.exe"
            : "SharedKernel.PlaywrightMcp.Tool";
        var startInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, fileName))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("serve");
        startInfo.Environment["PATH"] = path;
        foreach (var variable in IsolatedVariables)
        {
            startInfo.Environment.Remove(variable);
        }

        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("The Playwright MCP tool did not start.");
        }

        return new PlaywrightMcpToolProcess(process);
    }

    public async Task SendTerminationSignal(CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("kill -TERM \"$1\"");
        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add(_process.Id.ToString(CultureInfo.InvariantCulture));
        using var signal = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The termination signal could not be sent.");
        await signal.WaitForExitAsync(ct);
        if (signal.ExitCode != 0)
        {
            throw new InvalidOperationException($"Sending SIGTERM failed with exit code {signal.ExitCode}.");
        }
    }

    public async Task<ProcessResult> WaitForExit(CancellationToken ct)
    {
        await _process.WaitForExitAsync(ct);
        return new ProcessResult(_process.ExitCode, await _standardOutput, await _standardError);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(CancellationToken.None);
        }

        _process.Dispose();
    }
}
