using System.Data.Common;
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
    /// Adding the resource has no file system side effect. When the application starts (the <see cref="BeforeStartEvent"/>, not when publishing),
    /// the database file's parent directory is created if it is missing; the database file itself is created by SQLite on first use.
    /// The resource has a health check named <c>{name}-file</c>; a dependent resource that uses
    /// <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/> waits until the
    /// directory exists and the file, if present, is readable. The check does not test writability, because that cannot be done
    /// without writing; a read-only location surfaces as a SQLite error on first use.
    /// </remarks>
    public static IResourceBuilder<SqliteResource> AddSqlite(this IDistributedApplicationBuilder builder, [ResourceName] string name, string databasePath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath, builder.AppHostDirectory);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !builder.ExecutionContext.IsPublishMode)
        {
            builder.Eventing.Subscribe<BeforeStartEvent>((_, _) =>
            {
                Directory.CreateDirectory(directory);
                return Task.CompletedTask;
            });
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

    /// <summary>
    /// Gives a resource read-only access to a SQLite database file that another resource owns: it receives
    /// <c>ConnectionStrings__{connectionName}=Data Source=&lt;path&gt;;Mode=ReadOnly</c>, so SQLite refuses every write on that connection.
    /// </summary>
    /// <typeparam name="TDestination">The consuming resource type.</typeparam>
    /// <param name="builder">The consuming resource.</param>
    /// <param name="database">The database file resource.</param>
    /// <param name="connectionName">The connection string name; the database resource's name when omitted.</param>
    /// <returns>The consuming resource builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="database"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionName"/> is empty or white space.</exception>
    /// <remarks>
    /// The mode is a property of the connection string, so the consumer should still enforce it itself, for example by setting
    /// <c>Mode=ReadOnly</c> on whatever connection string it receives. Combine with <c>WaitFor</c> on the owner when the consumer needs the
    /// schema the owner creates.
    /// </remarks>
    public static IResourceBuilder<TDestination> WithReadOnlyReference<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<SqliteResource> database,
        string? connectionName = null)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);
        if (connectionName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        }

        var connectionString = new DbConnectionStringBuilder
        {
            ["Data Source"] = database.Resource.DatabasePath,
            ["Mode"] = "ReadOnly",
        }.ConnectionString;

        return builder
            .WithEnvironment($"ConnectionStrings__{connectionName ?? database.Resource.Name}", connectionString)
            .WithRelationship(database.Resource, "Reference");
    }
}
