using System.Diagnostics.CodeAnalysis;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// A group of baseline files or Git settings that <c>new</c> can write. Only the catalog values exist, so a parsed
/// feature is always valid.
/// </summary>
internal sealed record RepoFeature
{
    /// <summary>A README skeleton.</summary>
    public static readonly RepoFeature Readme = new("readme", selectedByDefault: true);

    /// <summary>Ignore rules; the language selects their content.</summary>
    public static readonly RepoFeature Gitignore = new("gitignore", selectedByDefault: true);

    /// <summary>Line-ending and binary-file attributes.</summary>
    public static readonly RepoFeature Gitattributes = new("gitattributes", selectedByDefault: true);

    /// <summary>Editor settings.</summary>
    public static readonly RepoFeature Editorconfig = new("editorconfig", selectedByDefault: true);

    /// <summary>A tracked <c>pre-commit</c> hook, activated through <c>core.hooksPath</c>.</summary>
    public static readonly RepoFeature Hooks = new("hooks", selectedByDefault: true);

    /// <summary>Neutral Git settings: default branch, fast-forward-only pulls, and pruning fetches.</summary>
    public static readonly RepoFeature GitConfig = new("git-config", selectedByDefault: true);

    /// <summary>
    /// Signed commits and tags over SSH. Not selected by default: signing needs a key that the caller provides, and
    /// commits fail without one.
    /// </summary>
    public static readonly RepoFeature SignedCommits = new("signed-commits", selectedByDefault: false);

    /// <summary>A neutral <c>AGENTS.md</c> skeleton, written once.</summary>
    public static readonly RepoFeature Agents = new("agents", selectedByDefault: true);

    /// <summary>A <c>global.json</c> that pins the .NET SDK, written once. Needs <c>--sdk-version</c>.</summary>
    public static readonly RepoFeature GlobalJson = new("global-json", selectedByDefault: true, RepoLanguage.Dotnet);

    /// <summary>A <c>NuGet.Config</c> that uses nuget.org only with source mapping, written once.</summary>
    public static readonly RepoFeature NugetConfig = new("nuget-config", selectedByDefault: true, RepoLanguage.Dotnet);

    /// <summary>A <c>Directory.Build.props</c> with strict compiler and analysis settings, written once. Needs <c>--sdk-version</c>.</summary>
    public static readonly RepoFeature BuildProps = new("build-props", selectedByDefault: true, RepoLanguage.Dotnet);

    private RepoFeature(string id, bool selectedByDefault, RepoLanguage? requiredLanguage = null)
    {
        Id = id;
        SelectedByDefault = selectedByDefault;
        RequiredLanguage = requiredLanguage;
    }

    /// <summary>Gets every feature, in the order that files and settings are written and messages list them.</summary>
    public static IReadOnlyList<RepoFeature> All { get; } = [Readme, Gitignore, Gitattributes, Editorconfig, Hooks, GitConfig, SignedCommits, Agents, GlobalJson, NugetConfig, BuildProps];

    /// <summary>Gets the ID that the <c>--feature</c> option takes.</summary>
    public string Id
    {
        get;
    }

    /// <summary>Gets whether <c>new</c> selects the feature when no <c>--feature</c> option is given and the language fits.</summary>
    public bool SelectedByDefault
    {
        get;
    }

    /// <summary>Gets the language that the feature needs, or <see langword="null"/> when it works with every language.</summary>
    public RepoLanguage? RequiredLanguage
    {
        get;
    }

    /// <summary>Gets whether the feature needs <c>--sdk-version</c>.</summary>
    public bool NeedsSdkVersion => this == GlobalJson || this == BuildProps;

    /// <summary>Gets the features that <c>new</c> selects for <paramref name="language"/> when no <c>--feature</c> option is given.</summary>
    public static IReadOnlyList<RepoFeature> DefaultsFor(RepoLanguage language) =>
        [.. All.Where(feature => feature.SelectedByDefault && (feature.RequiredLanguage is null || feature.RequiredLanguage == language))];

    /// <summary>Looks up the feature with the ID <paramref name="text"/>, compared exactly.</summary>
    /// <returns><see langword="false"/> when no feature has that ID.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out RepoFeature? feature)
    {
        feature = All.FirstOrDefault(candidate => string.Equals(candidate.Id, text, StringComparison.Ordinal));
        return feature is not null;
    }
}
