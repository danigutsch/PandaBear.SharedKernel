using System.Diagnostics.CodeAnalysis;

namespace PandaBear.IO;

/// <summary>
/// A repository-relative path that a configuration file names: segments separated by <c>/</c>, none of them empty,
/// <c>.</c>, or <c>..</c>, without a root, a <c>\</c>, or a <c>:</c>. It resolves under a root without following
/// symbolic links.
/// </summary>
public readonly record struct RepositoryPath
{
    private const string InvalidMessage = "Expected a repository-relative path with '/' separators and without empty, '.', or '..' segments.";

    private RepositoryPath(string value)
    {
        Value = value;
    }

    /// <summary>Gets the path text, with <c>/</c> separators.</summary>
    public string Value { get; }

    /// <summary>Reports whether <paramref name="value"/> is a valid repository-relative path.</summary>
    /// <param name="value">The text to check.</param>
    public static bool IsValid([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !Path.IsPathRooted(value)
        && !value.Contains('\\', StringComparison.Ordinal)
        && !value.Contains(':', StringComparison.Ordinal)
        && value.Split('/').All(static segment => segment is not ("" or "." or ".."));

    /// <summary>Creates a path from <paramref name="value"/> when it is valid.</summary>
    /// <param name="value">The text to parse.</param>
    /// <param name="path">The path, or the default value when <paramref name="value"/> is not valid.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a valid repository-relative path.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, out RepositoryPath path)
    {
        path = IsValid(value) ? new RepositoryPath(value) : default;
        return path.Value is not null;
    }

    /// <summary>Creates a path from <paramref name="value"/>.</summary>
    /// <param name="value">The text to parse.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="value"/> is not repository-relative. Configuration readers validate first with
    /// <see cref="IsValid"/>, so an invalid value here is a failed invariant of the caller, not a parse error.
    /// </exception>
    public static RepositoryPath Parse(string? value) =>
        TryParse(value, out var path) ? path : throw new InvalidOperationException(InvalidMessage);

    /// <summary>Resolves the path under <paramref name="root"/> without following symbolic links.</summary>
    /// <param name="root">The directory the path is relative to.</param>
    /// <returns>The combined path.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The path is the default value, or the path or one of its parent directories is a symbolic link.
    /// </exception>
    public string ResolveUnder(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (Value is null)
        {
            throw new InvalidOperationException(InvalidMessage);
        }

        var current = root;
        foreach (var segment in Value.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (new FileInfo(current).LinkTarget is not null || new DirectoryInfo(current).LinkTarget is not null)
            {
                throw new InvalidOperationException("Symbolic links are not followed in repository paths.");
            }
        }

        return current;
    }

    /// <summary>Returns the path text.</summary>
    public override string ToString() => Value ?? string.Empty;
}
