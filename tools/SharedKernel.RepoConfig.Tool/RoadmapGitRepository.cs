using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SharedKernel.RepoConfig.Tool;

internal sealed class RoadmapGitRepository
{
    private const int MaxErrorBytes = 16 * 1024;
    private const int MaxOutputBytes = 8 * 1024 * 1024;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(2);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly Func<ProcessStartInfo, CancellationToken, Task<RoadmapGitCommandResult>> _run;

    public RoadmapGitRepository()
        : this(RunProcess)
    {
    }

    internal RoadmapGitRepository(
        Func<ProcessStartInfo, CancellationToken, Task<RoadmapGitCommandResult>> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _run = run;
    }

    public async Task<string> ReadHead(string rootPath, CancellationToken cancellationToken)
    {
        var result = await Run(
            rootPath,
            ["rev-parse", "--verify", "HEAD"],
            cancellationToken).ConfigureAwait(false);
        string head;
        try
        {
            head = StrictUtf8.GetString(result.StandardOutput).Trim();
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidOperationException("Roadmap automation Git inspection returned invalid output.");
        }

        if (head.Length != 40 || !head.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw new InvalidOperationException("Roadmap automation Git inspection returned an invalid HEAD SHA.");
        }

        return head;
    }

    public async Task<byte[]> ReadStatus(string rootPath, CancellationToken cancellationToken)
    {
        var result = await Run(
            rootPath,
            ["status", "--porcelain=v2", "-z", "--untracked-files=all", "--ignore-submodules=none"],
            cancellationToken).ConfigureAwait(false);
        return result.StandardOutput;
    }

    private async Task<RoadmapGitCommandResult> Run(
        string rootPath,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var startInfo = CreateStartInfo(rootPath, arguments);
        var result = await _run(startInfo, cancellationToken).ConfigureAwait(false);
        if (result.OutputLimitExceeded || result.ErrorLimitExceeded || result.ExitCode != 0)
        {
            throw new InvalidOperationException("Roadmap automation Git inspection failed.");
        }

        return result;
    }

    private static ProcessStartInfo CreateStartInfo(string rootPath, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetFullPath(rootPath)
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment.Remove("GIT_ASKPASS");
        startInfo.Environment.Remove("SSH_ASKPASS");
        startInfo.Environment.Remove("GH_TOKEN");
        startInfo.Environment.Remove("GITHUB_TOKEN");
        return startInfo;
    }

    private static async Task<RoadmapGitCommandResult> RunProcess(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = new CancellationTokenSource(CommandTimeout);
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Roadmap automation could not start Git inspection.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception
            or InvalidOperationException
            or NotSupportedException)
        {
            throw new InvalidOperationException("Roadmap automation could not start Git inspection.");
        }

        var output = ReadBounded(process.StandardOutput.BaseStream, MaxOutputBytes);
        var error = ReadBounded(process.StandardError.BaseStream, MaxErrorBytes);
        try
        {
            await process.WaitForExitAsync(executionCancellation.Token).ConfigureAwait(false);
            var standardOutput = await output.ConfigureAwait(false);
            var standardError = await error.ConfigureAwait(false);
            return new RoadmapGitCommandResult(
                process.ExitCode,
                standardOutput.Content,
                Encoding.UTF8.GetString(standardError.Content),
                standardOutput.LimitExceeded,
                standardError.LimitExceeded);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await Terminate(process, output, error).ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await Terminate(process, output, error).ConfigureAwait(false);
            throw new TimeoutException($"Roadmap automation Git inspection timed out after {CommandTimeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} seconds.");
        }
        catch
        {
            await Terminate(process, output, error).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<(byte[] Content, bool LimitExceeded)> ReadBounded(Stream stream, int limit)
    {
        using var content = new MemoryStream();
        var buffer = new byte[8192];
        var limitExceeded = false;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            if (read == 0)
            {
                return (content.ToArray(), limitExceeded);
            }

            var remaining = limit - (int)content.Length;
            if (remaining > 0)
            {
                await content.WriteAsync(
                    buffer.AsMemory(0, Math.Min(remaining, read)),
                    CancellationToken.None).ConfigureAwait(false);
            }

            limitExceeded |= read > remaining;
        }
    }

    private static async Task Terminate(
        Process process,
        Task<(byte[] Content, bool LimitExceeded)> output,
        Task<(byte[] Content, bool LimitExceeded)> error)
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
            or OperationCanceledException)
        {
            // Preserve the original cancellation or timeout after best-effort termination.
        }

        var drain = Task.WhenAll(output, error);
        try
        {
            await drain.WaitAsync(TerminationTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException
            or IOException
            or InvalidOperationException)
        {
            _ = drain.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
