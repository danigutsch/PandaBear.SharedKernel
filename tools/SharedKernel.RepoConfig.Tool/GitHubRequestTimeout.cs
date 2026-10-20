using System.Globalization;

namespace SharedKernel.RepoConfig.Tool;

internal static class GitHubRequestTimeout
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    public static TimeoutException Create(string operation) => Create(operation, Duration);

    public static TimeoutException Create(string operation, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        var seconds = duration.TotalSeconds.ToString("0", CultureInfo.InvariantCulture);
        return new TimeoutException($"GitHub {operation} timed out after {seconds} seconds.");
    }
}
