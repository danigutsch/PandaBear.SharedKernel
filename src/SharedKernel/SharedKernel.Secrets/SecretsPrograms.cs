using PandaBear.Processes;

namespace PandaBear.Secrets;

/// <summary>
/// The programs that <see cref="SecretsRepository"/> runs, injected so a host passes the real ones and a test passes fakes.
/// <see cref="Age"/> and <see cref="Sops"/> are called only by the operations that need them, so a missing program fails
/// only those.
/// </summary>
public sealed record SecretsPrograms
{
    /// <summary>Gets the runner that starts <c>git</c> for <see cref="VerifySettings.BaseRevision"/>.</summary>
    public required IToolRunner Runner { get; init; }

    /// <summary>Gets the resolver that finds <c>git</c> in the absolute directories of <c>PATH</c>.</summary>
    public required IExecutableResolver ExecutableResolver { get; init; }

    /// <summary>Gets a factory of the age provider; it throws <see cref="InvalidOperationException"/> when a program is missing.</summary>
    public required Func<IAgeTool> Age { get; init; }

    /// <summary>Gets a factory of the sops provider; it throws <see cref="InvalidOperationException"/> when the program is missing.</summary>
    public required Func<ISopsTool> Sops { get; init; }

    /// <summary>Gets the longest a file read may take before the file counts as not readable, because a FIFO or a device would block. Five seconds by default.</summary>
    public TimeSpan FileReadTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
