namespace PandaBear.Mediator.Tests;

/// <summary>A scoped state holder that records its creation and disposal in a shared log.</summary>
internal sealed class ScopeMarker : IDisposable
{
    private readonly ScopeLog _log;

    /// <summary>Initializes a new instance of the <see cref="ScopeMarker"/> class.</summary>
    /// <param name="log">The shared log.</param>
    public ScopeMarker(ScopeLog log)
    {
        _log = log;
        _log.Entries.Add("marker created");
    }

    /// <summary>Gets or sets the value copied into the scope.</summary>
    public string? Value
    {
        get; set;
    }

    /// <inheritdoc />
    public void Dispose() => _log.Entries.Add("marker disposed");
}
