using SharedKernel.Processes;

namespace SharedKernel.Versioning.Tests;

/// <summary>The engine's external commands with the real process runner and the PATH-only executable resolver.</summary>
internal static class SystemCommands
{
    /// <summary>Gets commands that start real processes, as the tool does.</summary>
    public static ExternalCommands Instance { get; } = new(new SystemProcessRunner(), PathExecutableResolver.Default);
}
