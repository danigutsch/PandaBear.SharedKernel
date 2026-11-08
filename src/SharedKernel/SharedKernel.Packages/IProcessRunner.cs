namespace PandaBear.Packages;

/// <summary>Runs a child process. Tests replace it with a fake.</summary>
public interface IProcessRunner
{
    /// <summary>Runs <paramref name="executable"/> with <paramref name="arguments"/> and waits for it to exit.</summary>
    /// <param name="executable">The absolute path of the program.</param>
    /// <param name="arguments">The arguments, passed one by one without a shell.</param>
    /// <param name="cancellationToken">Cancels the wait and ends the process.</param>
    Task<ProcessOutput> Run(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
