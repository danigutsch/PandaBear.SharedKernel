using Microsoft.AspNetCore.Components.Server.Circuits;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Publishes the circuit's services in <see cref="CircuitServicesAccessor" /> for the duration of each inbound activity.
/// </summary>
internal sealed class ServicesAccessorCircuitHandler(IServiceProvider services) : CircuitHandler
{
    /// <inheritdoc />
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return async context =>
        {
            var previous = CircuitServicesAccessor.Services;
            CircuitServicesAccessor.Services = services;
            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                CircuitServicesAccessor.Services = previous;
            }
        };
    }
}
