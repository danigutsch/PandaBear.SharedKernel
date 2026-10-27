using System.Diagnostics.Metrics;

namespace PandaBear.AspNetCore.Tests;

/// <summary>A meter factory that cannot create a meter.</summary>
internal sealed class ThrowingMeterFactory : IMeterFactory
{
    /// <inheritdoc />
    public Meter Create(MeterOptions options) => throw new InvalidOperationException("factory-secret");

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
