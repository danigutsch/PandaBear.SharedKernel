using System.Diagnostics.CodeAnalysis;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Redirects standard error to a buffer until disposed.</summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The original writer is owned by the console and is only restored.")]
internal sealed class ConsoleErrorCapture : IDisposable
{
    private readonly TextWriter _original = Console.Error;
    private readonly StringWriter _buffer = new();

    /// <summary>Initializes a new instance of the <see cref="ConsoleErrorCapture" /> class and starts capturing.</summary>
    public ConsoleErrorCapture() => Console.SetError(_buffer);

    /// <summary>Gets the captured text.</summary>
    public string Text => _buffer.ToString();

    /// <inheritdoc />
    public void Dispose()
    {
        Console.SetError(_original);
        _buffer.Dispose();
    }
}
