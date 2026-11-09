using System.Globalization;
using System.Text.RegularExpressions;
using PandaBear.Tooling;

namespace PandaBear.Sandbox.Tool;

/// <summary>Validates the <c>settings</c> section. Errors name the file and the JSON path.</summary>
internal static partial class SettingsConfigurationValidator
{
    private const int MaxEntries = 100;
    private const int MaxEntryLength = 1024;

    /// <summary>Throws when <paramref name="settings"/> is not valid.</summary>
    /// <exception cref="InvalidOperationException">A value is out of range or malformed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static void Validate(SettingsConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Profile is not (null or DotnetSettingsProfile.Name))
        {
            throw Error("$.settings.profile", "must be \"dotnet\" when given");
        }

        if (settings.Target is not null && (!RepositoryPath.IsRelative(settings.Target) || !settings.Target.EndsWith(".json", StringComparison.Ordinal)))
        {
            throw Error("$.settings.target", "must be a repository-relative path with '/' separators that names a .json file");
        }

        if (settings.Profile is null && (settings.WriteNuGetFolders || settings.WriteDotnetHome || settings.AllowSdkDownload))
        {
            throw Error("$.settings", "writeNuGetFolders, writeDotnetHome, and allowSdkDownload need \"profile\": \"dotnet\"");
        }

        CheckList("allowWrite", settings.AllowWrite, IsSandboxPath, "an absolute path or start with '~/' or './'", rejectBroad: true);
        CheckList("denyWrite", settings.DenyWrite, IsSandboxPath, "an absolute path or start with '~/' or './'", rejectBroad: false);
        CheckList("denyRead", settings.DenyRead, IsSandboxPath, "an absolute path or start with '~/' or './'", rejectBroad: false);
        CheckList("removeDenyRead", settings.RemoveDenyRead, IsSandboxPath, "an absolute path or start with '~/' or './'", rejectBroad: false);
        CheckList("allowedDomains", settings.AllowedDomains, IsDomain, "a host name with an optional '*.' prefix and ':port' suffix", rejectBroad: false);
        CheckList("excludedCommands", settings.ExcludedCommands, static _ => true, "a command pattern", rejectBroad: false);
    }

    private static void CheckList(string name, string[]? entries, Func<string, bool> isValid, string expectation, bool rejectBroad)
    {
        if (entries is null)
        {
            return;
        }

        if (entries.Length > MaxEntries)
        {
            throw Error($"$.settings.{name}", $"must have at most {MaxEntries} entries");
        }

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var path = string.Create(CultureInfo.InvariantCulture, $"$.settings.{name}[{index}]");
            if (entry is not { Length: > 0 and <= MaxEntryLength } || entry.Any(char.IsControl) || !isValid(entry))
            {
                throw Error(path, $"must be {expectation}, at most {MaxEntryLength} characters, without control characters");
            }

            if (rejectBroad && Normalize(entry) is "/" or "~")
            {
                throw Error(path, "would make the whole filesystem or the whole home folder writable; name the folder that needs it");
            }
        }
    }

    // Drops empty and "." segments so that "/.", "//", "/./", and "~/." read as the root and the home folder.
    private static string Normalize(string entry)
    {
        var segments = entry.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(static segment => segment != ".");
        var joined = string.Join('/', segments);
        return entry.StartsWith('/') ? "/" + joined : entry.StartsWith("~/", StringComparison.Ordinal) ? joined.Length == 0 ? "~" : joined : joined;
    }

    private static readonly string[] GenericSecondLevelLabels = ["co", "com", "org", "net", "gov", "edu", "ac"];

    // A wildcard needs a registered name below it: "*.com" and "*.co.uk" would cover a whole public suffix. Without a
    // public suffix list the two-label case is a heuristic for the usual generic second-level names.
    private static bool IsDomain(string entry)
    {
        if (!Domain().IsMatch(entry))
        {
            return false;
        }

        if (!entry.StartsWith("*.", StringComparison.Ordinal))
        {
            return true;
        }

        var host = entry[2..];
        var colon = host.IndexOf(':', StringComparison.Ordinal);
        var labels = (colon < 0 ? host : host[..colon]).Split('.');
        return labels.Length >= 2 && !(labels.Length == 2 && GenericSecondLevelLabels.Contains(labels[0], StringComparer.Ordinal) && labels[1].Length == 2);
    }

    private static bool IsSandboxPath(string entry) =>
        (entry.StartsWith('/') || entry.StartsWith("~/", StringComparison.Ordinal) || entry.StartsWith("./", StringComparison.Ordinal))
        && !entry.Split('/').Contains("..");

    private static InvalidOperationException Error(string jsonPath, string message) =>
        new($"{SandboxConfigurationFile.RelativePath}: {jsonPath} {message}.");

    [GeneratedRegex(@"\A(?:\*\.)?[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?(?::[0-9]{1,5})?\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Domain();
}
