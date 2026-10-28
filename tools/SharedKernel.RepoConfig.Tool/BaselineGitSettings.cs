namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The Git settings and hook names that the baseline may manage. Anything that can run code or redirect
/// authentication is absent: <c>core.fsmonitor</c>, <c>core.sshCommand</c>, <c>core.pager</c>, <c>core.editor</c>,
/// <c>credential.*</c>, <c>alias.*</c>, <c>include*</c>, <c>url.*.insteadOf</c>, tool and filter drivers. The key
/// <c>core.hooksPath</c> is set only through the hooks section.
/// </summary>
internal static class BaselineGitSettings
{
    /// <summary>The key that the hooks section sets.</summary>
    public const string HooksPathKey = "core.hooksPath";

    /// <summary>The setting keys, compared ignoring case.</summary>
    public static readonly IReadOnlySet<string> AllowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "init.defaultBranch",
        "pull.ff",
        "pull.rebase",
        "push.autoSetupRemote",
        "push.default",
        "fetch.prune",
        "rebase.autoStash",
        "commit.gpgsign",
        "tag.gpgsign",
        "gpg.format",
        "user.useConfigOnly",
        "core.autocrlf",
        "core.eol",
    };

    /// <summary>The Git hook names a hook file may have.</summary>
    public static readonly IReadOnlySet<string> HookNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "pre-commit",
        "prepare-commit-msg",
        "commit-msg",
        "post-commit",
        "pre-merge-commit",
        "post-merge",
        "pre-rebase",
        "post-checkout",
        "pre-push",
    };
}
