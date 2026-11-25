using SharedKernel.Processes;
using SharedKernel.Results;

namespace SharedKernel.Secrets;

/// <summary>
/// The editor that <c>sops edit</c> starts, from the text of <c>VISUAL</c> or <c>EDITOR</c>: an absolute program path and
/// its arguments. The text is either the absolute path of an existing file, used as it is, or words separated by white
/// space whose first word is an absolute path or a command found in an absolute directory of <c>PATH</c>. Quotes are
/// not interpreted, so a word with a quote is refused, and so is a control character.
/// </summary>
public sealed class EditorCommand
{
    private EditorCommand(string executable, string[] arguments)
    {
        Executable = executable;
        Arguments = arguments;
    }

    /// <summary>Gets the absolute path of the editor program.</summary>
    public string Executable
    {
        get;
    }

    /// <summary>Gets the arguments that come before the file name.</summary>
    public IReadOnlyList<string> Arguments
    {
        get;
    }

    /// <summary>Creates the command.</summary>
    /// <param name="commandLine">The text of <c>VISUAL</c> or <c>EDITOR</c>.</param>
    /// <param name="executableResolver">Finds a command name in the absolute directories of <c>PATH</c>.</param>
    /// <returns>The command, or a validation error that never repeats the text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executableResolver"/> is <see langword="null"/>.</exception>
    public static Result<EditorCommand> Create(string? commandLine, IExecutableResolver executableResolver)
    {
        ArgumentNullException.ThrowIfNull(executableResolver);
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return Invalid("An editor command is required.");
        }

        if (commandLine.Any(static c => char.IsControl(c) && c != '\t'))
        {
            return Invalid("The editor command cannot contain control characters.");
        }

        if (Path.IsPathFullyQualified(commandLine) && File.Exists(commandLine))
        {
            return HasQuote(commandLine) ? QuotedPath() : Result.Ok(new EditorCommand(commandLine, []));
        }

        var words = commandLine.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(HasQuote))
        {
            return Invalid("The editor command cannot contain quotes; name a program and its arguments separated by spaces.");
        }

        var program = words[0];
        string? executable;
        if (Path.IsPathFullyQualified(program))
        {
            executable = File.Exists(program) ? program : null;
        }
        else
        {
            executable = program.Contains('/', StringComparison.Ordinal) || program.Contains('\\', StringComparison.Ordinal) ? null : executableResolver.Find(program);
        }

        if (executable is null)
        {
            return Invalid("The editor was not found: name an absolute path or a command in an absolute directory of PATH.");
        }

        return HasQuote(executable) ? QuotedPath() : Result.Ok(new EditorCommand(executable, words[1..]));
    }

    /// <summary>
    /// Returns the command as single-quoted words, the form in which <c>sops</c> reads <c>EDITOR</c> and splits it again
    /// without a shell.
    /// </summary>
    /// <returns>The quoted command.</returns>
    public string ToShellWords() => string.Join(' ', new[] { Executable }.Concat(Arguments).Select(static word => $"'{word}'"));

    private static bool HasQuote(string text) => text.Contains('\'', StringComparison.Ordinal) || text.Contains('"', StringComparison.Ordinal);

    private static Result<EditorCommand> QuotedPath() => Invalid("The editor's path cannot contain quotes, because sops splits the editor command again.");

    private static Result<EditorCommand> Invalid(string message) => Result.Invalid<EditorCommand>(message, "editor", message);
}
