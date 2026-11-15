namespace PandaBear.Sandbox;

/// <summary>A sandbox setting that <see cref="SettingsMerger"/> manages in an agent settings file.</summary>
public enum SandboxSetting
{
    /// <summary>The list of paths that sandboxed commands may write.</summary>
    AllowWrite,

    /// <summary>The list of paths that sandboxed commands may not write.</summary>
    DenyWrite,

    /// <summary>The list of paths that sandboxed commands may not read.</summary>
    DenyRead,

    /// <summary>The list of domains that sandboxed commands may reach.</summary>
    AllowedDomains,

    /// <summary>The list of command patterns that run outside the sandbox.</summary>
    ExcludedCommands,

    /// <summary>The flag that lets sandboxed commands connect to every Unix socket.</summary>
    AllowAllUnixSockets,

    /// <summary>The environment variables that the agent sets for its commands.</summary>
    Environment,
}
