using System.Diagnostics.CodeAnalysis;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// A language that selects the content of the features of <c>new</c>. Only the catalog values exist, so a parsed
/// language is always valid.
/// </summary>
internal sealed record RepoLanguage
{
    /// <summary>No language: language-neutral content.</summary>
    public static readonly RepoLanguage None = new("none");

    /// <summary>.NET: selects the .NET ignore rules.</summary>
    public static readonly RepoLanguage Dotnet = new("dotnet");

    private RepoLanguage(string id) => Id = id;

    /// <summary>Gets every language, in the order that messages list them.</summary>
    public static IReadOnlyList<RepoLanguage> All { get; } = [None, Dotnet];

    /// <summary>Gets the ID that the <c>--language</c> option takes.</summary>
    public string Id
    {
        get;
    }

    /// <summary>Looks up the language with the ID <paramref name="text"/>, compared exactly.</summary>
    /// <returns><see langword="false"/> when no language has that ID.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out RepoLanguage? language)
    {
        language = All.FirstOrDefault(candidate => string.Equals(candidate.Id, text, StringComparison.Ordinal));
        return language is not null;
    }
}
