namespace PandaBear.Sandbox;

/// <summary>The optional <c>settings</c> section of <c>.config/sharedkernel-sandbox.json</c>: what <c>sharedkernel-sandbox settings</c> manages in an agent settings file.</summary>
/// <param name="Profile">Name of a built-in profile; only <c>dotnet</c> exists. Without it nothing but the lists below is managed.</param>
/// <param name="Target">Settings file relative to the root; defaults to the agent format's own file, <c>.claude/settings.json</c> for Claude Code.</param>
/// <param name="AllowWrite">Entries for <c>sandbox.filesystem.allowWrite</c>.</param>
/// <param name="DenyWrite">Entries for <c>sandbox.filesystem.denyWrite</c>.</param>
/// <param name="DenyRead">Entries for <c>sandbox.filesystem.denyRead</c>. The top-level <c>deniedReadPaths</c> are always added too.</param>
/// <param name="AllowedDomains">Entries for <c>sandbox.network.allowedDomains</c>.</param>
/// <param name="ExcludedCommands">Entries for <c>sandbox.excludedCommands</c>; widens what runs unsandboxed, so it needs a risk flag.</param>
/// <param name="RemoveDenyRead">Entries removed from <c>sandbox.filesystem.denyRead</c>; needs a risk flag.</param>
/// <param name="AllowAllUnixSockets">Sets <c>sandbox.network.allowAllUnixSockets</c> to true when true; needs a risk flag.</param>
/// <param name="WriteNuGetFolders">Profile <c>dotnet</c>: allow writing the NuGet package folders.</param>
/// <param name="WriteDotnetHome">Profile <c>dotnet</c>: allow writing <c>~/.dotnet</c>.</param>
/// <param name="AllowSdkDownload">Profile <c>dotnet</c>: allow the domain that hosts SDK downloads.</param>
/// <param name="Environment">Profile <c>dotnet</c>: set the environment variables that keep build servers from outliving a command. Defaults to true.</param>
public sealed record SettingsConfiguration(
    string? Profile = null,
    string? Target = null,
    IReadOnlyList<string>? AllowWrite = null,
    IReadOnlyList<string>? DenyWrite = null,
    IReadOnlyList<string>? DenyRead = null,
    IReadOnlyList<string>? AllowedDomains = null,
    IReadOnlyList<string>? ExcludedCommands = null,
    IReadOnlyList<string>? RemoveDenyRead = null,
    bool AllowAllUnixSockets = false,
    bool WriteNuGetFolders = false,
    bool WriteDotnetHome = false,
    bool AllowSdkDownload = false,
    bool Environment = true);
