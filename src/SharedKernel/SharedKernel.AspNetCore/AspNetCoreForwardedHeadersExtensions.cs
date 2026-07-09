using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Provides reusable forwarded-header configuration helpers for ASP.NET Core applications.
/// </summary>
public static class AspNetCoreForwardedHeadersExtensions
{
    private const string KnownProxiesSectionName = "KnownProxies";

    private const string KnownNetworksSectionName = "KnownNetworks";

    /// <summary>
    /// Registers forwarded-header options with configured trusted proxy addresses/networks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing known proxies and networks.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddConfiguredTrustedForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ForwardedHeadersOptions>(options =>
            options.ConfigureTrustedForwardedHeaders(configuration));
        return services;
    }

    /// <summary>
    /// Configures forwarded headers and configured trusted proxy addresses/networks.
    /// </summary>
    /// <remarks>
    /// When at least one trusted proxy or network is configured, the default loopback trust entries are
    /// replaced with the configured entries. Network entries use CIDR notation, for example <c>10.0.0.0/8</c>.
    /// </remarks>
    /// <param name="options">The forwarded-header options to configure.</param>
    /// <param name="configuration">The configuration section containing known proxies and networks.</param>
    /// <returns>The same <see cref="ForwardedHeadersOptions"/> instance.</returns>
    public static ForwardedHeadersOptions ConfigureTrustedForwardedHeaders(
        this ForwardedHeadersOptions options,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        var knownProxies = GetValues(configuration.GetSection(KnownProxiesSectionName));
        var knownNetworks = GetValues(configuration.GetSection(KnownNetworksSectionName));
        if (knownProxies.Length == 0 && knownNetworks.Length == 0)
        {
            return options;
        }

        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var knownProxy in knownProxies)
        {
            options.KnownProxies.Add(ParseIPAddress(knownProxy, KnownProxiesSectionName));
        }

        foreach (var knownNetwork in knownNetworks)
        {
            options.KnownIPNetworks.Add(ParseIPNetwork(knownNetwork));
        }

        return options;
    }

    private static string[] GetValues(IConfiguration configuration)
    {
        return configuration.GetChildren()
            .Select(section => section.Value)
            .OfType<string>()
            .Select(static value => value.Trim())
            .Where(static value => value.Length > 0)
            .ToArray();
    }

    private static IPAddress ParseIPAddress(string value, string sectionName)
    {
        return IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException($"Security:ForwardedHeaders:{sectionName} contains an invalid IP address: {value}");
    }

    private static System.Net.IPNetwork ParseIPNetwork(string value)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && IPAddress.TryParse(parts[0], out var prefix)
            && int.TryParse(parts[1], out var prefixLength))
        {
            var maxPrefixLength = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            if (prefixLength >= 0 && prefixLength <= maxPrefixLength)
            {
                try
                {
                    return new System.Net.IPNetwork(prefix, prefixLength);
                }
                catch (ArgumentException ex)
                {
                    throw CreateInvalidNetworkException(value, ex);
                }
            }
        }

        throw CreateInvalidNetworkException(value);
    }

    private static InvalidOperationException CreateInvalidNetworkException(string value, Exception? innerException = null)
    {
        return new InvalidOperationException($"Security:ForwardedHeaders:{KnownNetworksSectionName} contains an invalid CIDR network: {value}", innerException);
    }
}
