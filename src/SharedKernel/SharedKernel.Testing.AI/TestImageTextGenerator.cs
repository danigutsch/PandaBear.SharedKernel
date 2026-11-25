using SharedKernel.AI;

namespace SharedKernel.Testing.AI;

/// <summary>Returns configured image accessibility text and records the most recent accepted request.</summary>
/// <param name="result">The result returned on a successful call.</param>
/// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
public sealed class TestImageTextGenerator(ImageTextGenerationResult result) : IImageTextGenerator
{
    private readonly ImageTextGenerationResult _result = result ?? throw new ArgumentNullException(nameof(result));
    private ImageTextGenerationRequest? _lastRequest;

    /// <summary>Gets the configured failure, which is thrown after a request is recorded.</summary>
    public Exception? Exception { get; init; }

    /// <summary>Gets an optional callback awaited before returning the fixed result.</summary>
    /// <remarks>The generator records the request before invoking this callback and passes the caller's cancellation token.
    /// A configured failure or a pre-cancelled token bypasses the callback.</remarks>
    public Func<CancellationToken, ValueTask>? BeforeResult { get; init; }

    /// <summary>Gets the most recent request, including any request that triggered the configured failure.</summary>
    public ImageTextGenerationRequest? LastRequest => Volatile.Read(ref _lastRequest);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is cancelled before acceptance or during the callback.</exception>
    public ValueTask<ImageTextGenerationResult> GenerateImageText(ImageTextGenerationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        Volatile.Write(ref _lastRequest, request);

        if (Exception is not null)
        {
            return ValueTask.FromException<ImageTextGenerationResult>(Exception);
        }

        return BeforeResult is { } callback ? WaitForCallback(callback, ct) : ValueTask.FromResult(_result);
    }

    private async ValueTask<ImageTextGenerationResult> WaitForCallback(Func<CancellationToken, ValueTask> callback, CancellationToken ct)
    {
        await callback(ct).ConfigureAwait(false);
        return ct.IsCancellationRequested
            ? await ValueTask.FromCanceled<ImageTextGenerationResult>(ct).ConfigureAwait(false)
            : _result;
    }
}
