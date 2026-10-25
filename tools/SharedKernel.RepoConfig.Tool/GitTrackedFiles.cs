using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Lists the files that the Git index of a repository tracks.
/// </summary>
internal static class GitTrackedFiles
{
    private const int BufferSize = 81_920;
    private const int MaxOutputBytes = 64 * 1024 * 1024;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(2);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Returns the paths of the regular files in the Git index of <paramref name="rootPath"/> that match one of the
    /// pathspecs, relative to the root, separated by <c>/</c>, once each, and in index order. Symbolic links and
    /// submodules are left out.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git cannot be found or started, or it cannot list the index.</exception>
    /// <exception cref="TimeoutException">Git does not finish within 30 seconds.</exception>
    public static async Task<IReadOnlyList<string>> Read(string rootPath, IReadOnlyList<string> pathspecs, CancellationToken cancellationToken)
    {
        List<string> paths = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var record in await ReadRecords(rootPath, ["ls-files", "-z", "--stage", "--", .. pathspecs], cancellationToken).ConfigureAwait(false))
        {
            // Each record is "<mode> <object> <stage>\t<path>"; an unmerged path has a record for each stage.
            var separator = record.IndexOf('\t', StringComparison.Ordinal);
            var mode = separator < 0 ? null : record.Split(' ', 2)[0];
            switch (mode)
            {
                case "100644" or "100755":
                    var path = record[(separator + 1)..];
                    if (seen.Add(path))
                    {
                        paths.Add(path);
                    }

                    break;
                case "120000" or "160000":
                    break;
                default:
                    throw Failure();
            }
        }

        return paths;
    }

    /// <summary>
    /// Returns the <c>git ls-files --eol</c> record of each entry in the Git index of <paramref name="rootPath"/>, in
    /// index order: <c>i/&lt;index&gt; w/&lt;working tree&gt; attr/&lt;attributes&gt;</c>, padded with spaces, then a tab
    /// and the path relative to the root.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git cannot be found or started, or it cannot list the index.</exception>
    /// <exception cref="TimeoutException">Git does not finish within 30 seconds.</exception>
    public static Task<string[]> ReadLineEndings(string rootPath, CancellationToken cancellationToken) =>
        ReadRecords(rootPath, ["ls-files", "-z", "--eol"], cancellationToken);

    private static async Task<string[]> ReadRecords(string rootPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var output = await Run(CreateStartInfo(rootPath, arguments), cancellationToken).ConfigureAwait(false);
        try
        {
            return StrictUtf8.GetString(output).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (DecoderFallbackException)
        {
            throw Failure();
        }
    }

    private static ProcessStartInfo CreateStartInfo(string rootPath, IReadOnlyList<string> arguments)
    {
        var git = ExecutableResolver.Resolve("git", Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows())
            ?? throw new InvalidOperationException("Could not resolve git to an absolute executable path.");
        ProcessStartInfo startInfo = new(git)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = rootPath,
        };

        // GIT_DIR, GIT_INDEX_FILE, and the GIT_*_PATHSPECS variables would select another index or change how paths match.
        foreach (var variable in startInfo.Environment.Keys.Where(static key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            startInfo.Environment.Remove(variable);
        }

        ChildProcessEnvironment.RemoveGitHubTokens(startInfo);
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static async Task<byte[]> Run(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = new CancellationTokenSource(CommandTimeout);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw Failure();
            }
        }
        catch (Exception exception) when (exception is Win32Exception or NotSupportedException)
        {
            throw Failure();
        }

        var output = ReadBounded(process.StandardOutput.BaseStream);
        var error = process.StandardError.BaseStream.CopyToAsync(Stream.Null, CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(execution.Token).ConfigureAwait(false);
            var (content, limitExceeded) = await output.ConfigureAwait(false);
            await error.ConfigureAwait(false);
            return process.ExitCode == 0 && !limitExceeded ? content : throw Failure();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await Terminate(process, output, error).ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await Terminate(process, output, error).ConfigureAwait(false);
            throw new TimeoutException("Git index inspection timed out after 30 seconds.");
        }
        catch (IOException)
        {
            await Terminate(process, output, error).ConfigureAwait(false);
            throw Failure();
        }
    }

    private static async Task<(byte[] Content, bool LimitExceeded)> ReadBounded(Stream stream)
    {
        var content = new MemoryStream();
        await using (content.ConfigureAwait(false))
        {
            var buffer = new byte[BufferSize];
            var limitExceeded = false;
            int read;
            while ((read = await stream.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false)) > 0)
            {
                // Output past the limit is drained so that Git never blocks on a full pipe.
                limitExceeded |= content.Length + read > MaxOutputBytes;
                if (!limitExceeded)
                {
                    await content.WriteAsync(buffer.AsMemory(0, read), CancellationToken.None).ConfigureAwait(false);
                }
            }

            return (content.ToArray(), limitExceeded);
        }
    }

    private static async Task Terminate(Process process, Task output, Task error)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var termination = new CancellationTokenSource(TerminationTimeout);
                await process.WaitForExitAsync(termination.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException
            or AggregateException
            or OperationCanceledException)
        {
            // Preserve the original cancellation, timeout, or failure after best-effort termination.
        }

        var drain = Task.WhenAll(output, error);
        try
        {
            await drain.WaitAsync(TerminationTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException
            or IOException
            or InvalidOperationException
            or OperationCanceledException)
        {
            _ = drain.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private static InvalidOperationException Failure() => new("Git index inspection failed.");
}
