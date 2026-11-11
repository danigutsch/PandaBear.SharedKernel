namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Carries the services of the Blazor Server circuit whose inbound activity is running, so that a
/// <see cref="DelegatingHandler" /> created in another dependency-injection scope by <see cref="IHttpClientFactory" />
/// can reach the circuit's authentication state.
/// </summary>
/// <remarks>This follows the documented "access server-side Blazor services from a different DI scope" pattern.</remarks>
internal static class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> CurrentServices = new();

    /// <summary>Gets or sets the services of the current circuit activity.</summary>
    internal static IServiceProvider? Services
    {
        get => CurrentServices.Value;
        set => CurrentServices.Value = value;
    }
}
