using System.Text;

namespace PandaBear.Npgsql.Testing.Host;

/// <summary>
/// The file in which the host tells its owner that the server accepts connections: it holds the server connection string
/// as one UTF-8 line and appears all at once, so a reader that sees the file sees the whole line.
/// </summary>
internal static class ReadyFile
{
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
        var path = readEnvironment(PostgreSqlTestServer.ReadyFileVariable)?.Trim();
        return string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(Path.GetDirectoryName(path)) || File.Exists(path)
            ? throw new InvalidOperationException($"The environment variable {PostgreSqlTestServer.ReadyFileVariable} must name a file that does not exist yet, by an absolute path in an existing directory.")
            : path;
    }

    /// <summary>Writes <paramref name="connectionString"/> to <paramref name="path"/> as one UTF-8 line without a byte order mark.</summary>
    /// <param name="path">The path that <see cref="Resolve"/> returned.</param>
    /// <param name="connectionString">The server connection string, which carries the password.</param>
    /// <remarks>
    /// The line is written to <c>&lt;path&gt;.tmp</c> first, readable by the current user only on Unix, and then moved
    /// over <paramref name="path"/>, so the owner never reads a partial line.
    /// </remarks>
    internal static void Publish(string path, string connectionString)
    {
        var temporary = path + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var writer = new StreamWriter(temporary, Utf8WithoutByteOrderMark, options))
        {
            writer.Write(connectionString);
            writer.Write('\n');
        }

        File.Move(temporary, path, overwrite: true);
    }
}
