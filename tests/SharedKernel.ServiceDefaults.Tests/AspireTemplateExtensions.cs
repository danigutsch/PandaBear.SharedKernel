using Microsoft.AspNetCore.Builder;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// The public methods of the official Aspire service-defaults template (<c>Extensions.cs</c> of the <c>aspire-servicedefaults</c>
/// template), in the template's namespace and with its signatures, so a test proves that the SharedKernel registration methods resolve
/// beside them. Each method only records that it ran.
/// </summary>
internal static class AspireTemplateExtensions
{
    /// <summary>Stands in for the template's <c>AddServiceDefaults</c>.</summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder => Record(builder, nameof(AddServiceDefaults));

    /// <summary>Stands in for the template's <c>ConfigureOpenTelemetry</c>.</summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder => Record(builder, nameof(ConfigureOpenTelemetry));

    /// <summary>Stands in for the template's <c>AddDefaultHealthChecks</c>.</summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder => Record(builder, nameof(AddDefaultHealthChecks));

    /// <summary>Stands in for the template's <c>MapDefaultEndpoints</c>.</summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ((IApplicationBuilder)app).Properties[nameof(MapDefaultEndpoints)] = true;
        return app;
    }

    private static TBuilder Record<TBuilder>(TBuilder builder, string method)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Properties[method] = true;
        return builder;
    }
}
