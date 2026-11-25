using System.Diagnostics.Metrics;

namespace SharedKernel.Testing.Metrics;

/// <summary>A meter factory that cannot create a meter.</summary>
public sealed class ThrowingMeterFactory : IMeterFactory
{
    /// <summary>Gets the message of the exception thrown by <see cref="Create" />.</summary>
    public const string FailureMessage = "factory-secret";

    /// <inheritdoc />
    public Meter Create(MeterOptions options) => throw new InvalidOperationException(FailureMessage);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
