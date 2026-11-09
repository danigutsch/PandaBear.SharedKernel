using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Testing.AspNetCore;

/// <summary>Starts applications on Kestrel for out-of-process test clients such as browsers.</summary>
public static class KestrelTestHost
{
    /// <summary>Starts the application's real entry point on Kestrel at a loopback address with an ephemeral port.</summary>
    /// <remarks>
    /// The listening address is supplied as the <c>urls</c> setting before the host is built, not through
    /// <c>WebApplicationFactory.UseKestrel(int)</c>. A minimal-hosting entry point keeps running after the factory obtains its host and starts
    /// that host on its own thread, while the factory's port override (<c>IServerAddressesFeature.Addresses.Clear()</c>) runs on the caller's
    /// thread. Under CPU contention the entry point wins and Kestrel rejects the change ("Addresses cannot be modified after the server has
    /// started"). A setting present before the host starts leaves nothing to race with.
    /// </remarks>
    /// <typeparam name="TEntryPoint">A type from the application's entry-point assembly, usually <c>Program</c>.</typeparam>
    /// <param name="environment">The hosting environment name, or <see langword="null"/> for the application's default.</param>
    /// <param name="configuration">Configuration values added on top of the application's own.</param>
    /// <param name="configureTestServices">Service overrides applied after the application's own registrations.</param>
    /// <returns>The running host.</returns>
    /// <exception cref="InvalidOperationException">The server published no listening address.</exception>
    public static KestrelTestHost<TEntryPoint> Start<TEntryPoint>(
        string? environment = null,
        IReadOnlyDictionary<string, string?>? configuration = null,
        Action<IServiceCollection>? configureTestServices = null)
        where TEntryPoint : class
    {
        var settings = configuration is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?>(configuration);
        settings["urls"] = "http://127.0.0.1:0";

        var factory = WebApplicationTestHost.Create<TEntryPoint>(environment, configureTestServices, null, settings);
        try
        {
            factory.UseKestrel();
            factory.StartServer();
            return new KestrelTestHost<TEntryPoint>(factory, ResolveAddress(factory.Services));
        }
        catch
        {
            factory.Dispose();
            throw;
        }
    }

    private static Uri ResolveAddress(IServiceProvider services)
    {
        var address = services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("The server published no listening address.");

        var builder = new UriBuilder(address);
        if (builder.Host is "0.0.0.0" or "[::]" or "::" or "+" or "*" or "localhost")
        {
            builder.Host = "127.0.0.1";
        }

        return builder.Uri;
    }
}
