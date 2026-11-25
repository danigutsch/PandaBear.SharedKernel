using System.Text;

namespace SharedKernel.TestServices.Host;

/// <summary>
/// The file in which the host tells its owner that its services are ready: it holds one UTF-8 <c>NAME=value</c> line for each
/// value it hands over, such as the server connection string under the variable the tests read, and appears all at once, so a
/// reader that sees the file sees every line.
/// </summary>
internal static class ReadyFile
{
    /// <summary>Names the environment variable that tells the host where to write its ready file.</summary>
    internal const string Variable = "SHAREDKERNEL_TESTING_SERVICES_READY_FILE";

    private static readonly UTF8Encoding Utf8WithoutByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Reads where the ready file goes and refuses a location that would fail or mislead after the server started.</summary>
    /// <param name="readEnvironment">Reads an environment variable; returns <see langword="null"/> when it is not set.</param>
    /// <returns>The absolute path of the ready file.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown, with a message that names the variable, when it is not set, is not an absolute path, lies in a directory
    /// that does not exist, or names a file that already exists. An existing file is refused rather than replaced
    /// because the owner would read it as the ready signal before the server of this run accepts connections.
    /// </exception>
    internal static string Resolve(Func<string, string?> readEnvironment)
    {
        var path = readEnvironment(Variable)?.Trim();
        return string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(Path.GetDirectoryName(path)) || File.Exists(path)
            ? throw new InvalidOperationException($"The environment variable {Variable} must name a file that does not exist yet, by an absolute path in an existing directory.")
            : path;
    }

    /// <summary>Writes one <c>NAME=value</c> line for each of <paramref name="values"/> to <paramref name="path"/>, as UTF-8 without a byte order mark.</summary>
    /// <param name="path">The path that <see cref="Resolve"/> returned.</param>
    /// <param name="values">
    /// The environment variables that the owner hands to the tests, with their values, such as the server connection string, which carries
    /// the password.
    /// </param>
    /// <exception cref="ArgumentException">
    /// A name is empty or holds <c>=</c> or a control character, or a value is empty or holds a control character, so it could not
    /// travel as one line. The message names the variable, never the value, and nothing is written.
    /// </exception>
    /// <remarks>
    /// The lines are written to <c>&lt;path&gt;.tmp</c> first, readable by the current user only on Unix, and then moved
    /// over <paramref name="path"/>, so the owner never reads a partial file.
    /// </remarks>
    internal static void Publish(string path, IReadOnlyList<(string Variable, string Value)> values)
    {
        foreach (var (variable, value) in values)
        {
            if (variable.Length == 0 || variable.Contains('=', StringComparison.Ordinal) || variable.Any(char.IsControl))
            {
                throw new ArgumentException("A variable name must be one non-empty line without '='.", nameof(values));
            }

            if (value.Length == 0 || value.Any(char.IsControl))
            {
                throw new ArgumentException($"The value of {variable} must be one non-empty line without control characters.", nameof(values));
            }
        }

        var temporary = path + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var writer = new StreamWriter(temporary, Utf8WithoutByteOrderMark, options))
        {
            foreach (var (variable, value) in values)
            {
                writer.Write(variable);
                writer.Write('=');
                writer.Write(value);
                writer.Write('\n');
            }
        }

        File.Move(temporary, path, overwrite: true);
    }
}
