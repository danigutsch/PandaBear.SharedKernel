namespace SharedKernel.Processes.Tests;

/// <summary>Signals the caller after writing child bytes to a test destination.</summary>
internal sealed class TestProcessOutputStream(Action onWrite) : MemoryStream
{
    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await base.WriteAsync(buffer, cancellationToken);
        onWrite();
    }
}
