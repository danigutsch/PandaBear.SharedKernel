using System.Text;

namespace SharedKernel.Secrets;

/// <summary>The result of a finished child process. Dispose it to overwrite the standard output bytes.</summary>
public sealed class ToolResult : IDisposable
{
    private const int MaxLineLength = 200;

    /// <summary>Creates a result.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="standardOutput">The standard output bytes; the result owns and clears them.</param>
    /// <param name="standardError">The first part of the standard error text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="standardOutput"/> or <paramref name="standardError"/> is <see langword="null"/>.</exception>
    public ToolResult(int exitCode, SecretBytes standardOutput, string standardError = "")
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>Gets the exit code.</summary>
    public int ExitCode
    {
        get;
    }

    /// <summary>Gets the standard output bytes, which can be plaintext.</summary>
    public SecretBytes StandardOutput
    {
        get;
    }

    /// <summary>Gets the first part of the standard error text. A tool does not print plaintext there, but treat it as unfiltered tool output.</summary>
    public string StandardError
    {
        get;
    }

    /// <summary>
    /// Describes a failed run in one short line that never carries standard output: the program, the exit code, and the
    /// first non-empty line of standard error with control characters replaced by <c>?</c>, cut at 200 characters.
    /// </summary>
    /// <param name="tool">The program name, such as <c>sops</c>.</param>
    /// <returns>The description.</returns>
    /// <exception cref="ArgumentException"><paramref name="tool"/> is <see langword="null"/>, empty, or white space.</exception>
    public string Describe(string tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        return $"{tool} exited with code {ExitCode}: {FirstLine(StandardError)}";
    }

    /// <inheritdoc />
    public void Dispose() => StandardOutput.Dispose();

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "no error output";
        StringBuilder clean = new();
        foreach (var character in line.Take(MaxLineLength))
        {
            clean.Append(char.IsControl(character) ? '?' : character);
        }

        return clean.ToString();
    }
}
