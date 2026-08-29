using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Provides reusable forwarded-header configuration helpers for ASP.NET Core applications.
/// </summary>
public static class AspNetCoreForwardedHeadersExtensions
{
    private const string ForwardedHeadersConfigurationSectionName = "Security:ForwardedHeaders";

    private const string KnownProxiesSectionName = "KnownProxies";

    private const string KnownNetworksSectionName = "KnownNetworks";

    private const string ForwardLimitSectionName = "ForwardLimit";

    /// <summary>
    /// Registers forwarded-header options from the standard trusted-proxy configuration section.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <typeparam name="TApplicationBuilder">The application builder type.</typeparam>
    /// <returns>The same application builder.</returns>
    public static TApplicationBuilder AddConfiguredTrustedForwardedHeaders<TApplicationBuilder>(
        this TApplicationBuilder builder)
        where TApplicationBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddConfiguredTrustedForwardedHeaders(
            builder.Configuration.GetSection(ForwardedHeadersConfigurationSectionName));
        return builder;
    }

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

        var knownProxiesSection = configuration.GetSection(KnownProxiesSectionName);
        var knownNetworksSection = configuration.GetSection(KnownNetworksSectionName);
        var knownProxies = GetValues(knownProxiesSection);
        var knownNetworks = GetValues(knownNetworksSection);
        var configuredForwardLimit = GetForwardLimit(configuration);
        if (knownProxies.Length == 0 && knownNetworks.Length == 0)
        {
            if (options.KnownProxies.Count == 0 && options.KnownIPNetworks.Count == 0)
            {
                options.KnownProxies.Add(IPAddress.Loopback);
                options.KnownProxies.Add(IPAddress.IPv6Loopback);
            }

            if (configuredForwardLimit is not null)
            {
                options.ForwardLimit = configuredForwardLimit.Value;
            }

            return options;
        }

        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        if (configuredForwardLimit is not null)
        {
            options.ForwardLimit = configuredForwardLimit.Value;
        }
        else if (knownProxies.Length > 0)
        {
            options.ForwardLimit = knownProxies.Length;
        }

        foreach (var knownProxy in knownProxies)
        {
            options.KnownProxies.Add(ParseIPAddress(knownProxy, knownProxiesSection.Path));
        }

        foreach (var knownNetwork in knownNetworks)
        {
            options.KnownIPNetworks.Add(ParseIPNetwork(knownNetwork, knownNetworksSection.Path));
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

    private static IPAddress ParseIPAddress(string value, string sectionPath)
    {
        return IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException($"{sectionPath} contains an invalid IP address: {value}");
    }

    private static int? GetForwardLimit(IConfiguration configuration)
    {
        var value = configuration[ForwardLimitSectionName];
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, out var forwardLimit) && forwardLimit > 0
            ? forwardLimit
            : throw new InvalidOperationException($"{configuration.GetSection(ForwardLimitSectionName).Path} must be a positive integer.");
    }

    private static System.Net.IPNetwork ParseIPNetwork(string value, string sectionPath)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && IPAddress.TryParse(parts[0], out var prefix)
            && int.TryParse(parts[1], out var prefixLength))
        {
            var maxPrefixLength = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            if (prefixLength > 0 && prefixLength <= maxPrefixLength)
            {
                try
                {
                    return new System.Net.IPNetwork(prefix, prefixLength);
                }
                catch (ArgumentException ex)
                {
                    throw CreateInvalidNetworkException(sectionPath, value, ex);
                }
            }
        }

        throw CreateInvalidNetworkException(sectionPath, value);
    }

    private static InvalidOperationException CreateInvalidNetworkException(string sectionPath, string value, Exception? innerException = null)
    {
        return new InvalidOperationException($"{sectionPath} contains an invalid CIDR network: {value}", innerException);
    }
}
