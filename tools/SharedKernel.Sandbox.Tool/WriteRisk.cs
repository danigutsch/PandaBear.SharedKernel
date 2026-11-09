namespace PandaBear.Sandbox.Tool;

/// <summary>What an <c>allowWrite</c> entry exposes beyond one tool's own folder.</summary>
[Flags]
internal enum WriteRisk
{
    /// <summary>Nothing beyond the named folder.</summary>
    None = 0,

    /// <summary>The home folder, a folder above it, or a folder at most one level below the root.</summary>
    Broad = 1,

    /// <summary>A shell startup file, an ssh or gpg folder, an autostart folder, or a git hook or configuration.</summary>
    Sensitive = 2,
}
