namespace PandaBear.Mediator.Tests;

/// <summary>A sender that answers with the value held by its scope's <see cref="ScopeMarker"/>.</summary>
internal sealed class MarkerSender : ISender
{
    private readonly ScopeMarker _marker;

    /// <summary>Initializes a new instance of the <see cref="MarkerSender"/> class.</summary>
    /// <param name="marker">The scoped state.</param>
    /// <param name="log">Where the creation of the sender is recorded.</param>
    public MarkerSender(ScopeMarker marker, ScopeLog log)
    {
        _marker = marker;
        log.Entries.Add("sender created");
    }

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct) =>
        ValueTask.FromResult((TResponse)(object)_marker.Value!);

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> Send<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct) =>
        throw new NotSupportedException();
}
