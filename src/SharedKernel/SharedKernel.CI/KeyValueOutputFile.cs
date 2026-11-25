using System.Text;

namespace SharedKernel.CI;

/// <summary>
/// Appends run outputs to a file as one <c>name=value</c> line each, the format of the <c>GITHUB_OUTPUT</c> file of GitHub Actions
/// and the <c>FORGEJO_OUTPUT</c> file of Forgejo Actions. A name that is empty or holds <c>=</c> or a line break, and a value that
/// holds a line break, are refused before anything is written, so a value cannot add an output of its own.
/// </summary>
public sealed class KeyValueOutputFile : IRunOutputs
{
    private readonly string _path;

    /// <summary>Creates the writer of the output file at <paramref name="path"/>.</summary>
    /// <param name="path">The absolute path of the file, such as the value of <c>GITHUB_OUTPUT</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is not fully qualified.</exception>
    public KeyValueOutputFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The output file must be a fully qualified path.", nameof(path));
        }

        _path = path;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="outputs"/> is <see langword="null"/>.</exception>
    public Task Write(IReadOnlyList<KeyValuePair<string, string>> outputs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        return File.AppendAllTextAsync(_path, Format(outputs), ct);
    }

    private static string Format(IReadOnlyList<KeyValuePair<string, string>> outputs)
    {
        var lines = new StringBuilder();
        foreach (var (name, value) in outputs)
        {
            if (string.IsNullOrEmpty(name) || name.AsSpan().IndexOfAny('=', '\r', '\n') >= 0)
            {
                throw new ArgumentException("An output name must not be empty or hold '=' or a line break.", nameof(outputs));
            }

            if (value is null || value.AsSpan().IndexOfAny('\r', '\n') >= 0)
            {
                throw new ArgumentException($"The value of output {name} must not be null or hold a line break.", nameof(outputs));
            }

            lines.Append(name).Append('=').Append(value).Append('\n');
        }

        return lines.ToString();
    }
}
