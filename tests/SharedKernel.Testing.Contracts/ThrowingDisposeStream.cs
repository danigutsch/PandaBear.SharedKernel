namespace SharedKernel.Testing.Contracts;

/// <summary>
/// A stream that throws a specified exception when asynchronously disposed.
/// </summary>
public sealed class ThrowingDisposeStream(Exception exception) : MemoryStream
{
    private readonly Exception exception = exception ?? throw new ArgumentNullException(nameof(exception));
    private bool hasThrown;

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        if (!hasThrown)
        {
            hasThrown = true;
            throw exception;
        }
    }
}
