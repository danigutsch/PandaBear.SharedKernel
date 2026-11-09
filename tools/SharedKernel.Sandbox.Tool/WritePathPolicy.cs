namespace PandaBear.Sandbox.Tool;

/// <summary>
/// Classifies an <c>allowWrite</c> entry. Paths are compared after dropping empty and <c>.</c> segments and after resolving
/// symbolic links, for the entry and for every known home folder, so a link or an alternative spelling cannot hide a home
/// folder.
/// </summary>
internal static class WritePathPolicy
{
    private static readonly string[][] SensitiveHomePaths =
    [
        [".bashrc"],
        [".bash_profile"],
        [".bash_login"],
        [".bash_logout"],
        [".profile"],
        [".zshrc"],
        [".zshenv"],
        [".zprofile"],
        [".ssh"],
        [".gnupg"],
        [".config", "autostart"],
    ];

    /// <summary>Classifies <paramref name="entry"/>.</summary>
    /// <param name="entry">An absolute path, or a path that starts with <c>~/</c> or <c>./</c>.</param>
    /// <param name="homes">The known home folders; empty entries are ignored.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static WriteRisk Classify(string entry, IReadOnlyList<string> homes)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(homes);

        var risk = WriteRisk.None;
        var tilde = entry.StartsWith("~/", StringComparison.Ordinal);
        var absolute = entry.StartsWith('/');
        var lexical = Segments(tilde ? entry[2..] : entry);
        if (HasSensitiveGit(lexical))
        {
            risk |= WriteRisk.Sensitive;
        }

        if (tilde)
        {
            return IsSensitiveHomePath(lexical) ? risk | WriteRisk.Sensitive : risk;
        }

        if (!absolute)
        {
            return risk;
        }

        List<string[]> entryForms = [lexical, Segments(Real(lexical))];
        List<string[]> homeForms = [];
        foreach (var home in homes.Where(static home => !string.IsNullOrWhiteSpace(home)))
        {
            var homeLexical = Segments(home.Replace('\\', '/'));
            homeForms.Add(homeLexical);
            homeForms.Add(Segments(Real(homeLexical)));
        }

        foreach (var form in entryForms)
        {
            if (form.Length <= 1 || homeForms.Any(home => StartsWith(home, form)))
            {
                risk |= WriteRisk.Broad;
            }

            if (HasSensitiveGit(form))
            {
                risk |= WriteRisk.Sensitive;
            }

            foreach (var home in homeForms.Where(home => StartsWith(form, home)))
            {
                if (IsSensitiveHomePath(form[home.Length..]))
                {
                    risk |= WriteRisk.Sensitive;
                }
            }
        }

        return risk;
    }

    private static string[] Segments(string path) =>
        [.. path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(static segment => segment != ".")];

    private static string Real(string[] segments)
    {
        var current = "/";
        foreach (var segment in segments)
        {
            var next = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
            if (info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
            {
                next = target.FullName;
            }

            current = next;
        }

        return current;
    }

    // True when the segments of prefix are the first segments of path (equal counts as a prefix).
    private static bool StartsWith(string[] path, string[] prefix) =>
        prefix.Length <= path.Length && prefix.Zip(path).All(static pair => string.Equals(pair.First, pair.Second, StringComparison.Ordinal));

    // A path under a sensitive entry, or an ancestor of one, such as ~/.config for ~/.config/autostart.
    private static bool IsSensitiveHomePath(string[] relative) =>
        SensitiveHomePaths.Any(sensitive => StartsWith(relative, sensitive) || StartsWith(sensitive, relative));

    // .git itself (which holds hooks and config), .git/hooks, and .git/config, and anything below them.
    private static bool HasSensitiveGit(string[] segments)
    {
        var index = Array.IndexOf(segments, ".git");
        return index >= 0 && (index == segments.Length - 1 || segments[index + 1] is "hooks" or "config");
    }
}
