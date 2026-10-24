using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

internal static class SecurityScanner
{
    public static Task<int> Run(string root, string destination, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        Run(root, destination, output, error,
            ExecutableResolver.Resolve("gitleaks", Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows()),
            RunProcess, cancellationToken);

    internal static async Task<int> Run(
        string root, string destination, TextWriter output, TextWriter error, string? executable,
        Func<ProcessStartInfo, CancellationToken, Task<int>> runProcess, CancellationToken cancellationToken)
    {
        if (executable is null)
        {
            await error.WriteLineAsync("security: Gitleaks is not installed.").ConfigureAwait(false);
            return 3;
        }

        var temporary = Directory.CreateTempSubdirectory("sharedkernel-security-");
        try
        {
            for (var directory = new DirectoryInfo(destination); directory is not null; directory = directory.Parent)
            {
                if (directory.LinkTarget is not null)
                {
                    throw new InvalidOperationException("Symbolic links are not supported in report paths.");
                }
            }

            var report = Path.Combine(temporary.FullName, "gitleaks.sarif");
            var startInfo = new ProcessStartInfo(executable)
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "dir", ".", "--redact=100", "--no-banner", "--exit-code", "10", "--report-format", "sarif", "--report-path", report })
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.Environment.Remove("GITLEAKS_CONFIG");
            startInfo.Environment.Remove("GITLEAKS_CONFIG_TOML");
            var exitCode = await runProcess(startInfo, cancellationToken).ConfigureAwait(false);
            if (exitCode is not (0 or 10))
            {
                await error.WriteLineAsync($"security: Gitleaks failed with exit code {exitCode.ToString(CultureInfo.InvariantCulture)}.").ConfigureAwait(false);
                return 3;
            }

            if (!File.Exists(report) || new FileInfo(report).Length > 20_971_520)
            {
                throw new InvalidOperationException("Missing or oversized scanner report.");
            }

            using var sarif = JsonDocument.Parse(await File.ReadAllTextAsync(report, cancellationToken).ConfigureAwait(false));
            if (!sarif.RootElement.TryGetProperty("version", out var version) || version.GetString() != "2.1.0"
                || !sarif.RootElement.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array || runs.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Invalid scanner report.");
            }

            var findings = runs.EnumerateArray().Sum(run => run.GetProperty("results").GetArrayLength());
            if ((exitCode == 0) != (findings == 0))
            {
                throw new InvalidOperationException("Inconsistent scanner result.");
            }

            Directory.CreateDirectory(destination);
            File.Move(report, Path.Combine(destination, "gitleaks.sarif"), overwrite: false);
            await output.WriteLineAsync($"Security scan completed: {findings.ToString(CultureInfo.InvariantCulture)} finding(s).").ConfigureAwait(false);
            return findings == 0 ? 0 : 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("security: Gitleaks was canceled.").ConfigureAwait(false);
            return 3;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
            or JsonException or KeyNotFoundException or Win32Exception or TimeoutException or NotSupportedException)
        {
            await error.WriteLineAsync("security: Gitleaks execution or report validation failed.").ConfigureAwait(false);
            return 3;
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    private static async Task<int> RunProcess(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Gitleaks.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            await Task.WhenAll(process.WaitForExitAsync(timeout.Token), stdout, stderr).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException exception)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cleanupTimeout.Token).ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException("Gitleaks timed out.", exception);
        }
    }
}
