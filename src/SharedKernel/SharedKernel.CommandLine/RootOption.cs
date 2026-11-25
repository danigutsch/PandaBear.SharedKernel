using System.CommandLine;
using System.CommandLine.Parsing;
using SharedKernel.InputNormalization;

namespace SharedKernel.CommandLine;

/// <summary>
/// The <c>--root</c> option of a tool: the repository root directory, which defaults to the working directory. An empty
/// or white-space value is a parse error, so that an unset shell variable cannot select the working directory, and a
/// relative path resolves against the working directory.
/// </summary>
public sealed class RootOption : Option<string>
{
    /// <summary>The description that <see cref="RootOption(string)"/> gives the option.</summary>
    public const string DefaultDescription = "Repository root directory. Defaults to the current directory.";

    /// <summary>The parse error for an empty or white-space value.</summary>
    public const string EmptyMessage = "--root requires a directory path.";

    /// <summary>The parse error for a value with a null character, which a response file can supply.</summary>
    public const string NullCharacterMessage = "--root path contains a null character.";

    private const string MissingDirectoryMessage = "--root is not an existing directory: ";

    private readonly string _workingDirectory;

    /// <summary>Creates the option with <see cref="DefaultDescription"/>.</summary>
    /// <param name="workingDirectory">The absolute directory that a relative root and the default root resolve from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="workingDirectory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="workingDirectory"/> is not an absolute path.</exception>
    public RootOption(string workingDirectory)
        : this(workingDirectory, DefaultDescription)
    {
    }

    /// <summary>Creates the option.</summary>
    /// <param name="workingDirectory">The absolute directory that a relative root and the default root resolve from.</param>
    /// <param name="description">The help text, which says what the root holds.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="workingDirectory"/> is not an absolute path.</exception>
    public RootOption(string workingDirectory, string description)
        : base("--root")
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(description);
        if (!Path.IsPathFullyQualified(workingDirectory))
        {
            throw new ArgumentException("The working directory must be an absolute path.", nameof(workingDirectory));
        }

        _workingDirectory = Path.GetFullPath(workingDirectory);
        Description = description;
        HelpName = "path";
        CustomParser = Parse;
    }

    /// <summary>Returns the absolute root: the parsed value, or the working directory without the option.</summary>
    /// <param name="parseResult">A parse result without errors.</param>
    /// <returns>The absolute root, which need not exist, for a command that creates it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parseResult"/> is <see langword="null"/>.</exception>
    public string GetDirectory(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        return parseResult.GetValue(this) ?? _workingDirectory;
    }

    /// <summary>Returns the absolute root, as <see cref="GetDirectory"/> does, when it is an existing directory.</summary>
    /// <param name="parseResult">A parse result without errors.</param>
    /// <returns>The absolute root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parseResult"/> is <see langword="null"/>.</exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The root is not an existing directory. The message, the same in every tool, names the root with its control
    /// characters escaped.
    /// </exception>
    public string GetExistingDirectory(ParseResult parseResult)
    {
        var directory = GetDirectory(parseResult);
        return Directory.Exists(directory)
            ? directory
            : throw new DirectoryNotFoundException(MissingDirectoryMessage + ControlCharacters.Escape(directory));
    }

    private string? Parse(ArgumentResult result)
    {
        var path = result.Tokens is [.., var token] ? token.Value : string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            result.AddError(EmptyMessage);
            return null;
        }

        if (path.Contains('\0', StringComparison.Ordinal))
        {
            result.AddError(NullCharacterMessage);
            return null;
        }

        return Path.GetFullPath(path, _workingDirectory);
    }
}
