using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Runs the child processes of <c>verify-devcontainer</c> and copies their output, line by line as it arrives, to
/// writers and an optional log file.
/// </summary>
internal static class DevcontainerProcess
{
    /// <summary>
    /// Runs <paramref name="startInfo"/> without the GitHub tokens and returns its exit code. Cancellation kills the
    /// process tree, waits for its output to close, and rethrows.
    /// </summary>
    /// <param name="startInfo">The process to start; its file name is a fully qualified path.</param>
    /// <param name="output">Receives standard output.</param>
    /// <param name="error">Receives standard error.</param>
    /// <param name="logPath">A file that receives both streams, or <see langword="null"/>.</param>
    /// <param name="ct">Cancels the process.</param>
    public static async Task<int> Run(ProcessStartInfo startInfo, TextWriter output, TextWriter error, string? logPath, CancellationToken ct)
    {
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        ChildProcessEnvironment.RemoveGitHubTokens(startInfo);
        ct.ThrowIfCancellationRequested();

        using var process = Start(startInfo);
        process.StandardInput.Close();
        var copy = Copy(process.StandardOutput, process.StandardError, output, error, logPath);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited after the wait was canceled.
            }
        }

        // Like a shell pipe, the copy ends when every process holding the output has closed it.
        await copy.ConfigureAwait(false);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return process.ExitCode;
    }

    private static Process Start(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"Could not start {startInfo.FileName}.", exception);
        }
    }

    private static async Task Copy(StreamReader standardOutput, StreamReader standardError, TextWriter output, TextWriter error, string? logPath)
    {
        var log = logPath is null ? TextWriter.Null : new StreamWriter(logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };
        await using (log.ConfigureAwait(false))
        {
            using var gate = new SemaphoreSlim(1, 1);
            await Task.WhenAll(CopyLines(standardOutput, output, log, gate), CopyLines(standardError, error, log, gate)).ConfigureAwait(false);
        }
    }

    private static async Task CopyLines(StreamReader reader, TextWriter writer, TextWriter log, SemaphoreSlim gate)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await writer.WriteLineAsync(line).ConfigureAwait(false);
                await log.WriteLineAsync(line).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
