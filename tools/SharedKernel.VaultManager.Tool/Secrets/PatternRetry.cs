using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Runs pattern matches that can exceed their match timeout. A run that times out is run once more before the match
/// counts as timed out: a pause of the whole process, such as a garbage collection on a machine that is short of
/// processor time, rarely repeats, while catastrophic backtracking on the same text times out again.
/// </summary>
internal static class PatternRetry
{
    /// <summary>Runs <paramref name="run"/>, and runs it once more when a pattern match in it times out.</summary>
    /// <typeparam name="T">Result of the run.</typeparam>
    /// <param name="run">Match work that starts from scratch each time it runs and changes no state outside its result.</param>
    /// <param name="result">The result of the run that completed.</param>
    /// <returns><see langword="true"/> when a run completed; <see langword="false"/> when both runs timed out.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="run"/> is <see langword="null"/>.</exception>
    public static bool TryRun<T>(Func<T> run, [MaybeNullWhen(false)] out T result)
    {
        ArgumentNullException.ThrowIfNull(run);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                result = run();
                return true;
            }
            catch (RegexMatchTimeoutException)
            {
                // The first timeout is retried; the second one is the caller's to report.
            }
        }

        result = default;
        return false;
    }
}
