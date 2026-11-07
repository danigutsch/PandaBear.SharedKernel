using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding SQLite resources to the application model.
/// </summary>
public static class SqliteResourceExtensions
{
    /// <summary>
    /// Adds a SQLite database file to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="databasePath">The path of the database file; a relative path is resolved against the AppHost directory.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="databasePath"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <remarks>
    /// The database file's parent directory is created if it is missing; the database file itself is created by SQLite on first use.
    /// The resource has a health check named <c>{name}-file</c>; a dependent resource that uses
    /// <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/> waits until the
    /// directory is writable and the file, if present, is readable.
    /// </remarks>
    public static IResourceBuilder<SqliteResource> AddSqlite(this IDistributedApplicationBuilder builder, [ResourceName] string name, string databasePath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath, builder.AppHostDirectory);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var resource = new SqliteResource(name, fullPath);
        var healthCheckName = $"{name}-file";
        builder.Services.AddHealthChecks().AddCheck(healthCheckName, new SqliteFileHealthCheck(fullPath));

        return builder.AddResource(resource)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Sqlite",
                State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success),
                Properties = [new ResourcePropertySnapshot("DatabasePath", fullPath)],
            })
            .WithHealthCheck(healthCheckName);
    }
}
