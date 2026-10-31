using Npgsql;

namespace PandaBear.Npgsql;

/// <summary>
/// Provides reusable privacy-safe tracing configuration for Npgsql data sources.
/// </summary>
public static class NpgsqlDataSourceBuilderExtensions
{
    /// <summary>
    /// Keeps Npgsql tracing enabled while omitting the optional first-response event.
    /// </summary>
    /// <param name="builder">The Npgsql data source builder to configure.</param>
    /// <returns>The configured data source builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static NpgsqlDataSourceBuilder ConfigureTracingWithoutFirstResponseEvent(
        this NpgsqlDataSourceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureTracing(tracing => tracing.EnableFirstResponseEvent(enable: false));

        return builder;
    }
}
