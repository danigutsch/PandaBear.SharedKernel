namespace PandaBear.Sandbox.Tool;

/// <summary>The optional consumer policy in <c>.config/sharedkernel-sandbox.json</c>.</summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="DeniedReadPaths">
/// Paths that the sandbox must not let a command read, such as credential folders. Each is absolute or starts with
/// <c>~/</c>. The tool embeds no path of its own.
/// </param>
/// <param name="Settings">Optional: what <c>sharedkernel-sandbox settings</c> manages in an agent settings file.</param>
internal sealed record SandboxConfiguration(int Version, string[] DeniedReadPaths, SettingsConfiguration? Settings = null);
