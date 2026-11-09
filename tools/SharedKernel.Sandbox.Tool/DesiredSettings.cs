using System.Globalization;

namespace PandaBear.Sandbox.Tool;

/// <summary>What the configuration, its profile, and the accepted risks ask the settings file to contain.</summary>
/// <param name="Target">The settings file relative to the root.</param>
/// <param name="AllowWrite">Entries for <c>sandbox.filesystem.allowWrite</c>.</param>
/// <param name="DenyWrite">Entries for <c>sandbox.filesystem.denyWrite</c>.</param>
/// <param name="DenyRead">Entries for <c>sandbox.filesystem.denyRead</c>.</param>
/// <param name="AllowedDomains">Entries for <c>sandbox.network.allowedDomains</c>.</param>
/// <param name="ExcludedCommands">Entries for <c>sandbox.excludedCommands</c>.</param>
/// <param name="RemoveDenyRead">Entries to remove from <c>denyRead</c>.</param>
/// <param name="AllowAllUnixSockets">Whether <c>allowAllUnixSockets</c> is to be set to true.</param>
/// <param name="Environment">The <c>env</c> entries to add.</param>
/// <param name="RiskWarnings">The risks whose flags were not passed, when the refusal is not enforced.</param>
internal sealed record DesiredSettings(
    string Target,
    IReadOnlyList<string> AllowWrite,
    IReadOnlyList<string> DenyWrite,
    IReadOnlyList<string> DenyRead,
    IReadOnlyList<string> AllowedDomains,
    IReadOnlyList<string> ExcludedCommands,
    IReadOnlyList<string> RemoveDenyRead,
    bool AllowAllUnixSockets,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> RiskWarnings)
{
    /// <summary>The default settings file, relative to the root.</summary>
    public const string DefaultTarget = ".claude/settings.json";

    /// <summary>Builds the desired entries and refuses what needs a risk flag that is not set.</summary>
    /// <param name="configuration">The configuration with a <c>settings</c> section.</param>
    /// <param name="flags">The accepted risks.</param>
    /// <param name="homeDirectories">The known home folders of the user, so that <c>allowWrite</c> entries for them are caught.</param>
    /// <param name="enforce">Whether a missing risk flag refuses (<see langword="true"/>) or only becomes a warning in <see cref="RiskWarnings"/>.</param>
    /// <exception cref="InvalidOperationException">A risky request lacks its flag, or a removal contradicts what the configuration requires.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static DesiredSettings Build(SandboxConfiguration configuration, SettingsRiskFlags flags, IReadOnlyList<string> homeDirectories, bool enforce)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(flags);
        ArgumentNullException.ThrowIfNull(homeDirectories);
        var settings = configuration.Settings
            ?? throw new InvalidOperationException($"{SandboxConfigurationFile.RelativePath}: $.settings is required for the settings command.");

        var excluded = Distinct(settings.ExcludedCommands);
        var removed = Distinct(settings.RemoveDenyRead);
        List<(string Flag, bool Accepted, string Message)> risks = [];
        if (excluded.Length > 0)
        {
            risks.Add((
                "--accept-risk-excluded-commands",
                flags.ExcludedCommands,
                "$.settings.excludedCommands would let those commands run outside the sandbox with full access to the account's files and network."));
        }

        if (settings.AllowAllUnixSockets)
        {
            risks.Add((
                "--accept-risk-allow-all-unix-sockets",
                flags.AllowAllUnixSockets,
                "$.settings.allowAllUnixSockets would let every sandboxed command connect to every Unix socket: the container engine socket (full control of the host), the ssh and gpg agents, and the user session bus."));
        }

        if (removed.Length > 0)
        {
            risks.Add((
                "--accept-risk-remove-deny-read",
                flags.RemoveDenyRead,
                "$.settings.removeDenyRead would make paths readable that the sandbox currently hides, such as credential folders."));
        }

        var allowWriteEntries = settings.AllowWrite ?? [];
        for (var index = 0; index < allowWriteEntries.Length; index++)
        {
            var risk = WritePathPolicy.Classify(allowWriteEntries[index], homeDirectories);
            var location = string.Create(CultureInfo.InvariantCulture, $"$.settings.allowWrite[{index}]");
            if (risk.HasFlag(WriteRisk.Broad))
            {
                risks.Add((
                    "--accept-risk-broad-write",
                    flags.BroadWrite,
                    location + " would make the home folder, a folder above it, or a top-level system folder writable: sandboxed commands could rewrite shell startup files, ssh and gpg configuration, and other tools' state."));
            }

            if (risk.HasFlag(WriteRisk.Sensitive))
            {
                risks.Add((
                    "--accept-risk-sensitive-write",
                    flags.SensitiveWrite,
                    location + " would make a shell startup file, an ssh or gpg folder, an autostart folder, or a git hook or configuration writable: code planted there runs later, outside the sandbox."));
            }
        }

        List<string> riskWarnings = [];
        foreach (var (flag, accepted, message) in risks.Where(static risk => !risk.Accepted))
        {
            if (enforce)
            {
                throw new InvalidOperationException($"Refused: {message} Pass {flag} to accept that.");
            }

            riskWarnings.Add($"would require {flag}: {message}");
        }

        var profile = settings.Profile is DotnetSettingsProfile.Name;
        List<string> allowWrite = [.. settings.AllowWrite ?? []];
        List<string> domains = [.. settings.AllowedDomains ?? []];
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        if (profile)
        {
            domains.Add(DotnetSettingsProfile.FeedDomain);
            if (settings.AllowSdkDownload)
            {
                domains.Add(DotnetSettingsProfile.SdkDomain);
            }

            if (settings.WriteNuGetFolders)
            {
                allowWrite.AddRange(DotnetSettingsProfile.NuGetFolders);
            }

            if (settings.WriteDotnetHome)
            {
                allowWrite.Add(DotnetSettingsProfile.DotnetHome);
            }

            if (settings.Environment)
            {
                foreach (var (name, value) in DotnetSettingsProfile.Environment)
                {
                    environment[name] = value;
                }
            }
        }

        var denyRead = Distinct([.. configuration.DeniedReadPaths, .. settings.DenyRead ?? []]);
        var contradiction = removed.FirstOrDefault(entry => denyRead.Contains(entry, SettingsMerger.PathComparer));
        return contradiction is not null
            ? throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"{SandboxConfigurationFile.RelativePath}: $.settings.removeDenyRead names a path that $.deniedReadPaths or $.settings.denyRead requires."))
            : new DesiredSettings(
                settings.Target ?? DefaultTarget,
                Distinct(allowWrite),
                Distinct(settings.DenyWrite),
                denyRead,
                Distinct(domains),
                excluded,
                removed,
                settings.AllowAllUnixSockets,
                environment,
                riskWarnings);
    }

    private static string[] Distinct(IEnumerable<string>? values) => [.. (values ?? []).Distinct(StringComparer.Ordinal)];
}
