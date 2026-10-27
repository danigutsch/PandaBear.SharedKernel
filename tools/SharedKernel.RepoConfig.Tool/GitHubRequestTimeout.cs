using System.Globalization;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Timeout applied to GitHub requests.</summary>
internal static class GitHubRequestTimeout
{
    /// <summary>Default time allowed for a GitHub request.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    /// <summary>Creates the timeout exception for an operation that exceeded the default duration.</summary>
    /// <param name="operation">Description of the operation that timed out.</param>
    public static TimeoutException Create(string operation) => Create(operation, Duration);

    /// <summary>Creates the timeout exception for an operation that exceeded the given duration.</summary>
    /// <param name="operation">Description of the operation that timed out.</param>
    /// <param name="duration">Duration that was exceeded; must be positive.</param>
    public static TimeoutException Create(string operation, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        var seconds = duration.TotalSeconds.ToString("0", CultureInfo.InvariantCulture);
        return new TimeoutException($"GitHub {operation} timed out after {seconds} seconds.");
    }
}
