using System.Text;

namespace PandaBear.CI.Forgejo;

/// <summary>Formats run outputs as <c>name=value</c> lines.</summary>
internal static class OutputLines
{
    /// <summary>Returns one <c>name=value</c> line, ending with <c>\n</c>, for each output.</summary>
    /// <exception cref="ArgumentException">A name is empty or holds <c>=</c> or a line break, or a value holds a line break.</exception>
    public static string Format(IReadOnlyList<KeyValuePair<string, string>> outputs)
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
