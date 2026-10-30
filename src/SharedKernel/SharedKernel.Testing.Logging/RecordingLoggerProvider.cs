using Microsoft.Extensions.Logging;

namespace PandaBear.Testing.Logging;

/// <summary>An <see cref="ILoggerProvider" /> whose loggers all write into one shared <see cref="RecordingLogger" />, so records from every category keep their logging order.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly Lock _gate = new();
    private readonly List<string> _categoryNames = [];

    /// <summary>Gets the logger that receives every record created through this provider.</summary>
    public RecordingLogger Logger { get; } = new();

    /// <summary>Gets a snapshot of the captured records in logging order.</summary>
    public IReadOnlyList<RecordedLog> Records => Logger.Records;

    /// <summary>Gets a snapshot of the category names passed to <see cref="CreateLogger" /> in call order.</summary>
    public IReadOnlyList<string> CategoryNames
    {
        get
        {
            lock (_gate)
            {
                return [.. _categoryNames];
            }
        }
    }

    /// <summary>Records <paramref name="categoryName" /> and returns the shared <see cref="Logger" />.</summary>
    /// <param name="categoryName">The category requested by the caller.</param>
    /// <returns>The shared recording logger.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        lock (_gate)
        {
            _categoryNames.Add(categoryName);
        }

        return Logger;
    }

    /// <summary>Releases no resources.</summary>
    public void Dispose()
    {
    }
}
