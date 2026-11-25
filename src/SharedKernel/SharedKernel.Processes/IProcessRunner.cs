using System.Diagnostics;

namespace SharedKernel.Processes;

/// <summary>Runs a child process to completion. Hosts use <see cref="SystemProcessRunner"/> or <see cref="BoundedProcessRunner"/>; tests replace it with a fake.</summary>
public interface IProcessRunner
{
    /// <summary>Runs the process that <paramref name="startInfo"/> describes and waits for it to exit.</summary>
    /// <param name="startInfo">The process to run. Its <see cref="ProcessStartInfo.FileName"/> is a fully qualified path.</param>
    /// <param name="forward">
    /// Receives each line of the redirected standard output and standard error as it arrives, and the result then holds
    /// no output; <see langword="null"/> collects the output in the result.
    /// </param>
    /// <param name="cancellationToken">Cancels the run and ends the process tree.</param>
    /// <returns>The exit code and the collected output.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="startInfo"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the file name of <paramref name="startInfo"/> is not a fully qualified path.</exception>
    /// <exception cref="InvalidOperationException">The process could not be started.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled; the process tree has been ended.</exception>
    Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken);
}
