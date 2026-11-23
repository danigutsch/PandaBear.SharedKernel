namespace ObservabilityConsumer;

/// <summary>An instrumentation object that records whether the provider holding it disposed it, so a test sees that the provider received it.</summary>
internal sealed class ProbeInstrumentation : IDisposable
{
    /// <summary>Gets a value indicating whether the provider disposed this instrumentation.</summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc />
    public void Dispose() => Disposed = true;
}
