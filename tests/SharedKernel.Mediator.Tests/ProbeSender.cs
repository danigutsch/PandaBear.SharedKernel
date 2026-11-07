namespace PandaBear.Mediator.Tests;

/// <summary>A sender that answers with the id of its own scope and reports its disposal.</summary>
/// <param name="probe">Where disposal is recorded.</param>
internal sealed class ProbeSender(ScopeProbe probe) : ISender, IDisposable
{
    private readonly Guid _id = Guid.NewGuid();

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct) =>
        ValueTask.FromResult((TResponse)(object)_id);

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> Send<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public void Dispose() => probe.DisposedScopes.Add(_id);
}
