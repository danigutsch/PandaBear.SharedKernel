using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Repositories;

/// <summary>
/// A language that selects the content of the features of <c>new</c>. Only the catalog values exist, so a parsed
/// language is always valid.
/// </summary>
public sealed record RepositoryLanguage
{
    /// <summary>No language: language-neutral content.</summary>
    public static readonly RepositoryLanguage None = new("none");

    /// <summary>.NET: selects the .NET ignore rules.</summary>
    public static readonly RepositoryLanguage Dotnet = new("dotnet");

    /// <summary>Shell: explicitly normalizes shell script and test files to LF.</summary>
    public static readonly RepositoryLanguage Shell = new("shell");

    /// <summary>Nix: ignores the default local build output links.</summary>
    public static readonly RepositoryLanguage Nix = new("nix");

    private RepositoryLanguage(string id) => Id = id;

    /// <summary>Gets every language, in the order that messages list them.</summary>
    public static IReadOnlyList<RepositoryLanguage> All { get; } = [None, Dotnet, Shell, Nix];

    /// <summary>Gets the ID that the <c>--language</c> option takes.</summary>
    public string Id { get; }

    /// <summary>Looks up the language with the ID <paramref name="text"/>, compared exactly.</summary>
    /// <returns><see langword="false"/> when no language has that ID.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out RepositoryLanguage? language)
    {
        language = All.FirstOrDefault(candidate => string.Equals(candidate.Id, text, StringComparison.Ordinal));
        return language is not null;
    }
}
