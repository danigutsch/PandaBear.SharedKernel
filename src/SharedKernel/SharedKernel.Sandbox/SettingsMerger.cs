namespace PandaBear.Sandbox;

/// <summary>
/// Merges the desired entries into an agent settings file. Only the entries the tool is asked for are added (or, for
/// <c>denyRead</c> with a risk flag, removed); every other key and every other list entry stays as it is. Nothing is ever
/// removed because it is absent from the configuration.
/// </summary>
public static class SettingsMerger
{
    /// <summary>Compares paths ignoring a trailing slash.</summary>
    internal static readonly IEqualityComparer<string> PathComparer = new TrailingSlashComparer();

    /// <summary>Compares <paramref name="document"/> with <paramref name="desired"/>, edits the document, and returns the edits and the resulting text.</summary>
    /// <param name="desired">The entries the settings file should contain.</param>
    /// <param name="document">The settings file, opened in its own format; it is edited in place.</param>
    /// <exception cref="InvalidOperationException">A managed setting has the wrong type, or the document cannot be written back. The message names the file and the location.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static SettingsPlan Plan(DesiredSettings desired, ISandboxSettingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(document);

        List<SettingsChange> changes = [];
        List<string> warnings = [];

        AddMissing(document, SandboxSetting.AllowWrite, desired.AllowWrite, PathComparer, changes);
        AddMissing(document, SandboxSetting.DenyWrite, desired.DenyWrite, PathComparer, changes);
        AddMissing(document, SandboxSetting.DenyRead, desired.DenyRead, PathComparer, changes);
        AddMissing(document, SandboxSetting.AllowedDomains, desired.AllowedDomains, StringComparer.OrdinalIgnoreCase, changes);
        AddMissing(document, SandboxSetting.ExcludedCommands, desired.ExcludedCommands, StringComparer.Ordinal, changes);
        RemoveEntries(document, SandboxSetting.DenyRead, desired.RemoveDenyRead, changes);
        SetSockets(document, desired.AllowAllUnixSockets, changes, warnings);
        MergeEnvironment(document, desired.Environment, changes, warnings);

        var content = changes.Count == 0 ? document.Text ?? string.Empty : document.Serialize();
        return new SettingsPlan(changes, warnings, content, document.Text);
    }

    private static void AddMissing(ISandboxSettingsDocument document, SandboxSetting setting, IReadOnlyList<string> wanted, IEqualityComparer<string> comparer, List<SettingsChange> changes)
    {
        var present = new HashSet<string>(document.GetEntries(setting), comparer);
        foreach (var entry in wanted)
        {
            if (!present.Add(entry))
            {
                continue;
            }

            document.AddEntry(setting, entry);
            changes.Add(new SettingsChange(SettingsChangeKind.Add, document.LocationOf(setting), entry));
        }
    }

    private static void RemoveEntries(ISandboxSettingsDocument document, SandboxSetting setting, IReadOnlyList<string> unwanted, List<SettingsChange> changes)
    {
        foreach (var entry in unwanted)
        {
            var entries = document.GetEntries(setting);
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                if (PathComparer.Equals(entries[index], entry))
                {
                    document.RemoveEntry(setting, index);
                    changes.Add(new SettingsChange(SettingsChangeKind.Remove, document.LocationOf(setting), entry));
                }
            }
        }
    }

    private static void SetSockets(ISandboxSettingsDocument document, bool wanted, List<SettingsChange> changes, List<string> warnings)
    {
        var location = document.LocationOf(SandboxSetting.AllowAllUnixSockets);
        if (document.GetFlag(SandboxSetting.AllowAllUnixSockets) is not { } enabled)
        {
            // Never replace a value of another type silently.
            if (wanted)
            {
                throw new InvalidOperationException($"{document.Name}: {location} must be a boolean.");
            }

            warnings.Add($"{location} is not a boolean; the tool leaves it as it is.");
            return;
        }

        if (wanted && !enabled)
        {
            document.SetFlag(SandboxSetting.AllowAllUnixSockets);
            changes.Add(new SettingsChange(SettingsChangeKind.Set, location, "true"));
        }
        else if (!wanted && enabled)
        {
            warnings.Add($"{location} is true in the file; the tool does not manage it and leaves it as it is.");
        }
    }

    private static void MergeEnvironment(ISandboxSettingsDocument document, IReadOnlyDictionary<string, string> wanted, List<SettingsChange> changes, List<string> warnings)
    {
        var location = document.LocationOf(SandboxSetting.Environment);
        foreach (var (name, value) in wanted)
        {
            if (document.TryGetEnvironmentVariable(name, out var current))
            {
                if (!string.Equals(current, value, StringComparison.Ordinal))
                {
                    warnings.Add($"{location}.{name} has a different value; the tool leaves it as it is.");
                }

                continue;
            }

            document.SetEnvironmentVariable(name, value);
            changes.Add(new SettingsChange(SettingsChangeKind.Add, location, name));
        }
    }

    private sealed class TrailingSlashComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => string.Equals(Normalize(x), Normalize(y), StringComparison.Ordinal);

        public int GetHashCode(string obj) => Normalize(obj).GetHashCode(StringComparison.Ordinal);

        private static string Normalize(string? value) => value is { Length: > 1 } ? value.TrimEnd('/') is { Length: > 0 } trimmed ? trimmed : "/" : value ?? string.Empty;
    }
}
