using System.Text;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

/// <summary>Records text written from any thread and reports the text written through the end of the first line.</summary>
internal sealed class FirstLineWriter : TextWriter
{
    private readonly Lock _gate = new();
    private readonly StringBuilder _text = new();
    private readonly TaskCompletionSource<string> _firstLine = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override Encoding Encoding => Encoding.UTF8;

    /// <summary>Completes with the text written through the first line terminator.</summary>
    public Task<string> FirstLine => _firstLine.Task;

    /// <inheritdoc />
    public override void Write(char value)
    {
        lock (_gate)
        {
            _text.Append(value);
            if (value == '\n')
            {
                _firstLine.TrySetResult(_text.ToString());
            }
        }
    }

    /// <summary>Returns the text written so far.</summary>
    public override string ToString()
    {
        lock (_gate)
        {
            return _text.ToString();
        }
    }
}
