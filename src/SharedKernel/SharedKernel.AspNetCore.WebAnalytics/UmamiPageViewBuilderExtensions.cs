using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>Adds Umami as a page-view target when its connection string is present.</summary>
public static class UmamiPageViewBuilderExtensions
{
    /// <summary>The connection name <see cref="AddUmami"/> reads by default: <c>ConnectionStrings:umami</c>.</summary>
    public const string DefaultConnectionName = "umami";

    /// <summary>
    /// The name of the <c>HttpClient</c> that sends page views to Umami. It gets the host's HTTP client defaults, such as the resilience
    /// and telemetry of the service defaults; configure it further with <c>services.AddHttpClient(HttpClientName)</c>.
    /// </summary>
    public const string HttpClientName = "SharedKernel.AspNetCore.WebAnalytics.Umami";

    /// <summary>
    /// Adds a self-hosted Umami as a page-view target when the connection string <c>ConnectionStrings:{connectionName}</c> is present, in
    /// the form <c>Endpoint=&lt;Umami's base address&gt;;WebsiteId=&lt;the website id&gt;</c> that the Umami website resource of
    /// <c>PandaBear.SharedKernel.Aspire.Hosting.Umami</c> hands to a site with <c>WithReference(website, connectionName: "umami")</c>.
    /// Without it, or when it is blank, nothing is added and the site runs without Umami.
    /// </summary>
    /// <param name="builder">The page-view builder.</param>
    /// <param name="connectionName">The connection name, <see cref="DefaultConnectionName"/> by default; it is also the target's name.</param>
    /// <param name="configure">Further Umami settings, or <see langword="null"/>.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionName"/> is blank.</exception>
    /// <remarks>
    /// A connection string that is present is checked when the host starts: one whose endpoint is not an absolute http or https address
    /// without user info, query or fragment, or whose website id is not a GUID, stops the host with an
    /// <see cref="OptionsValidationException"/> that names the key and never repeats the value. The client's logged header values are
    /// redacted, so the visitor's <c>User-Agent</c> and address never reach the HTTP client logs.
    /// </remarks>
    public static PageViewBuilder AddUmami(this PageViewBuilder builder, string connectionName = DefaultConnectionName, Action<UmamiPageViewTargetOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var connectionString = builder.Configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return builder;
        }

        var services = builder.Services;
        services.AddOptions<UmamiPageViewTargetOptions>(connectionName)
            .Configure(options => options.ConnectionString = connectionString)
            .ValidateOnStart();
        if (configure is not null)
        {
            services.Configure(connectionName, configure);
        }

        services.AddSingleton<IValidateOptions<UmamiPageViewTargetOptions>>(new UmamiPageViewTargetOptionsValidator(connectionName));
        services.AddHttpClient(HttpClientName).RedactLoggedHeaders(static _ => true);
        return builder.AddTarget(serviceProvider => UmamiPageViewTarget.Create(serviceProvider, connectionName));
    }
}
