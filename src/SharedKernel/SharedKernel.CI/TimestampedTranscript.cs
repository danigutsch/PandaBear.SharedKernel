using System.Globalization;
using System.Text;

namespace PandaBear.CI;

/// <summary>
/// Receives the forwarded output lines of a child process, writes each to the echo writer with its UTC time as it
/// arrives, and keeps the lines without the time.
/// </summary>
internal sealed class TimestampedTranscript(TextWriter echo, TimeProvider timeProvider) : TextWriter
{
    private readonly StringBuilder _text = new();
    private readonly StringBuilder _line = new();

    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <summary>Gets the lines received so far, each ending with <see cref="Environment.NewLine"/>.</summary>
    public string Text
    {
        get
        {
            lock (_text)
            {
                return _text.ToString();
            }
        }
    }

    /// <inheritdoc />
    public override void Write(char value)
    {
        lock (_text)
        {
            if (value == '\n')
            {
                Complete(_line.ToString().TrimEnd('\r'));
                _line.Clear();
            }
            else
            {
                _line.Append(value);
            }
        }
    }

    /// <inheritdoc />
    public override void WriteLine(string? value)
    {
        lock (_text)
        {
            Complete(_line.Append(value).ToString());
            _line.Clear();
        }
    }

    /// <inheritdoc />
    public override Task WriteLineAsync(string? value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }

    private void Complete(string line)
    {
        _text.AppendLine(line);
        echo.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[{timeProvider.GetUtcNow():HH:mm:ss}] {line}"));
    }
}
