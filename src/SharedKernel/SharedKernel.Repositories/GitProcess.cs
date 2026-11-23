using System.Diagnostics;
using System.Globalization;
using PandaBear.Processes;

namespace PandaBear.Repositories;

/// <summary>
/// Runs every <c>git</c> call of this library the same way: start information from <see cref="GitStartInfo"/>, and a
/// <see cref="BoundedProcessRunner"/> with the <see cref="GitStartInfo.Timeout"/> and a 64 KiB standard error limit.
/// </summary>
/// <param name="resolver">Finds the <c>git</c> executable.</param>
/// <param name="timeout">The time limit of one call.</param>
internal sealed class GitProcess(IExecutableResolver resolver, TimeSpan timeout)
{
    /// <summary>The most standard error bytes a <c>git</c> call may write.</summary>
    internal const int MaxErrorBytes = 64 * 1024;

    private readonly BoundedProcessRunner _runner = new(timeout);

    /// <summary>Gets the runner that every production caller uses: <c>git</c> from the absolute <c>PATH</c> directories, stopped after 30 seconds.</summary>
    public static GitProcess Default { get; } = new(PathExecutableResolver.Default, GitStartInfo.Timeout);

    /// <summary>Gets the time limit of one call.</summary>
    public TimeSpan Timeout { get; } = timeout;

    /// <summary>Gets the time limit in whole seconds, for messages.</summary>
    public string TimeoutSeconds => Timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Describes a <c>git</c> run in <paramref name="workingDirectory"/>.</summary>
    /// <exception cref="InvalidOperationException"><c>git</c> cannot be found in an absolute <c>PATH</c> directory.</exception>
    public ProcessStartInfo CreateStartInfo(string workingDirectory, IEnumerable<string> arguments) =>
        GitStartInfo.Create(workingDirectory, arguments, resolver);

    /// <summary>Runs <paramref name="startInfo"/> with <paramref name="standardInput"/> and at most <paramref name="maxOutputBytes"/> of standard output.</summary>
    /// <exception cref="InvalidOperationException"><c>git</c> cannot start, writes more than a limit, or leaves its output open.</exception>
    /// <exception cref="TimeoutException"><c>git</c> runs longer than <see cref="Timeout"/>; it has been stopped.</exception>
    public Task<CapturedProcessOutput> Run(
        ProcessStartInfo startInfo,
        int maxOutputBytes,
        ReadOnlyMemory<byte> standardInput,
        CancellationToken cancellationToken) =>
        _runner.Capture(startInfo, standardInput, maxOutputBytes, MaxErrorBytes, cancellationToken);
}
