namespace PandaBear.Sandbox.Tool;

/// <summary>The explicit acceptances that let the settings command widen what a sandbox allows.</summary>
/// <param name="AllowAllUnixSockets">Accept <c>allowAllUnixSockets: true</c>.</param>
/// <param name="ExcludedCommands">Accept entries in <c>excludedCommands</c>.</param>
/// <param name="RemoveDenyRead">Accept removing entries from <c>denyRead</c>.</param>
/// <param name="BroadWrite">Accept an <c>allowWrite</c> entry for the home folder, a folder above it, or a top-level system folder.</param>
/// <param name="SensitiveWrite">Accept an <c>allowWrite</c> entry for shell startup files, ssh and gpg folders, autostart, or git hooks and configuration.</param>
internal sealed record SettingsRiskFlags(bool AllowAllUnixSockets, bool ExcludedCommands, bool RemoveDenyRead, bool BroadWrite = false, bool SensitiveWrite = false);
