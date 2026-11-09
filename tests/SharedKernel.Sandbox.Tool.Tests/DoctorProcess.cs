using System.Diagnostics;
using System.Globalization;

namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>
/// Runs the built tool as a child process with a chosen temporary folder. .NET computes the socket folder of a named pipe once
/// per process from <c>TMPDIR</c>, so a test that changes it in-process would depend on which test used a pipe first.
/// </summary>
internal static class DoctorProcess
{
    /// <summary>Runs <c>doctor</c> with <c>TMPDIR</c> set to <paramref name="temporaryFolder"/> and returns what it printed.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(string temporaryFolder, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(SystemSandboxEnvironment).Assembly.Location);
        startInfo.ArgumentList.Add("doctor");
        startInfo.ArgumentList.Add("--root");
        startInfo.ArgumentList.Add(AppContext.BaseDirectory);
        startInfo.Environment["TMPDIR"] = temporaryFolder;
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet did not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, (await output).ToString(CultureInfo.InvariantCulture), await error);
    }
}
