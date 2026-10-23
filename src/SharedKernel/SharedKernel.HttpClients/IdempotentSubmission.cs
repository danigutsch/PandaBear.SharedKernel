using PandaBear.Idempotency;

namespace PandaBear.HttpClients;

/// <summary>Retains one explicit HTTP command and idempotency key while its commit outcome is unknown.</summary>
/// <typeparam name="T">The response contract.</typeparam>
/// <remarks>
/// The caller must capture immutable command inputs and use an API that enforces the supplied idempotency key.
/// Retries are explicit; no background request is scheduled. State is in memory and does not survive process restart.
/// </remarks>
public sealed class IdempotentSubmission<T>
{
    private readonly Lock _gate = new();
    private Func<string, CancellationToken, Task<HttpOutcome<T>>>? _pending;
    private IdempotencyKey? _key;
    private string _operation = string.Empty;
    private bool _isBusy;

    /// <summary>Gets whether a request is currently executing.</summary>
    public bool IsBusy
    {
        get
        {
            lock (_gate)
            {
                return _isBusy;
            }
        }
    }
    /// <summary>Gets whether the same command and key must be retried before accepting another command.</summary>
    public bool HasPending
    {
        get
        {
            lock (_gate)
            {
                return _pending is not null;
            }
        }
    }
    /// <summary>Gets the caller's non-sensitive label for the current or last operation.</summary>
    public string Operation
    {
        get
        {
            lock (_gate)
            {
                return _operation;
            }
        }
    }

    /// <summary>Starts one command with a new opaque key, or returns null while another command is busy or pending.</summary>
    /// <param name="operation">A non-sensitive operation label.</param>
    /// <param name="submit">The callback capturing the exact immutable request to send with the generated key.</param>
    /// <param name="ct">The request cancellation token.</param>
    /// <returns>The HTTP outcome, or null when no request was started.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> or <paramref name="submit"/> is null.</exception>
    public Task<HttpOutcome<T>?> Submit(string operation, Func<string, CancellationToken, Task<HttpOutcome<T>>> submit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(submit);
        lock (_gate)
        {
            if (_isBusy || _pending is not null)
            {
                return Task.FromResult<HttpOutcome<T>?>(null);
            }
            _pending = submit;
            _key = IdempotencyKey.From(Guid.CreateVersion7().ToString("N"));
            _operation = operation;
            _isBusy = true;
        }
        return Execute(ct);
    }

    /// <summary>Explicitly retries the retained command with its original key, or returns null when busy or empty.</summary>
    /// <param name="ct">The retry cancellation token.</param>
    /// <returns>The HTTP outcome, or null when no request was started.</returns>
    public Task<HttpOutcome<T>?> Retry(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_isBusy || _pending is null)
            {
                return Task.FromResult<HttpOutcome<T>?>(null);
            }
            _isBusy = true;
        }
        return Execute(ct);
    }

    private async Task<HttpOutcome<T>?> Execute(CancellationToken ct)
    {
        Func<string, CancellationToken, Task<HttpOutcome<T>>> pending;
        IdempotencyKey key;
        lock (_gate)
        {
            if (_pending is null || _key is not { } retainedKey)
            {
                _isBusy = false;
                return null;
            }
            pending = _pending;
            key = retainedKey;
        }
        try
        {
            var result = await pending(key.Value, ct).ConfigureAwait(false);
            if (result.Kind is HttpOutcomeKind.Succeeded or HttpOutcomeKind.ValidationProblem or HttpOutcomeKind.NotFound
                or HttpOutcomeKind.Unauthorized or HttpOutcomeKind.Forbidden or HttpOutcomeKind.Conflict)
            {
                lock (_gate)
                {
                    _pending = null;
                    _key = null;
                }
            }
            return result;
        }
        finally { lock (_gate) { _isBusy = false; } }
    }
}
