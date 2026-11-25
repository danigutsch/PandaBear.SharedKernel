using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>A fake clock, a recording cache and a data protection provider for ticket store tests.</summary>
internal sealed class TicketStoreTestContext
{
    /// <summary>The fake clock's start.</summary>
    internal static readonly DateTimeOffset Start = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The fake clock.</summary>
    internal FakeTimeProvider Time { get; } = new(Start);

    /// <summary>The recording cache.</summary>
    internal RecordingDistributedCache Cache { get; } = new();

    /// <summary>The data protection provider of the application.</summary>
    internal IDataProtectionProvider DataProtection { get; } = new EphemeralDataProtectionProvider();

    /// <summary>Creates a store over the context's cache and clock.</summary>
    /// <param name="configure">Configures the options.</param>
    /// <param name="dataProtection">Another application's data protection, if given.</param>
    /// <returns>The store.</returns>
    internal ServerSideTicketStore CreateStore(
        Action<ServerSideTicketStoreOptions>? configure = null,
        IDataProtectionProvider? dataProtection = null)
    {
        var options = new ServerSideTicketStoreOptions();
        configure?.Invoke(options);
        return new ServerSideTicketStore(
            Cache,
            dataProtection ?? DataProtection,
            Options.Create(options),
            Time,
            new RecordingLogger<ServerSideTicketStore>());
    }

    /// <summary>Creates a cookie ticket issued now.</summary>
    /// <param name="subject">The subject.</param>
    /// <param name="expiresIn">The ticket lifetime, or <see langword="null" /> for no expiry.</param>
    /// <returns>The ticket.</returns>
    internal AuthenticationTicket CreateTicket(string subject, TimeSpan? expiresIn = null)
    {
        var properties = new AuthenticationProperties
        {
            IssuedUtc = Time.GetUtcNow(),
            ExpiresUtc = expiresIn is null ? null : Time.GetUtcNow().Add(expiresIn.Value)
        };
        return new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "oidc")),
            properties,
            CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
