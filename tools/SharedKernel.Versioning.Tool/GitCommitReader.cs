using System.Diagnostics;

namespace SharedKernel.Versioning.Tool;

internal static class GitCommitReader
{
    public static async Task<IReadOnlyList<string>> ReadMessages(string? since)
    {
        var range = string.IsNullOrWhiteSpace(since) ? "HEAD" : $"{since}..HEAD";
        using var process = Process.Start(new ProcessStartInfo("git", ["log", "--format=%B%x00", range])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Could not start git.");

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(error.Trim());
        }

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
