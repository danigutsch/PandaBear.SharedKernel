using System.Data.Common;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding SQLite resources to the application model.
/// </summary>
public static class SqliteResourceExtensions
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>
    /// Adds an ephemeral SQLite database file, <c>{name}.db</c>, to the application model. Use
    /// <see cref="WithDataBindMount(IResourceBuilder{SqliteResource}, string)"/> to keep the data across runs.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource; the database file is <c>{name}.db</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <remarks>
    /// <para>
    /// Like a container without a data volume, the database is ephemeral: its <see cref="SqliteResource.DataDirectory"/> is a new
    /// directory under <see cref="Path.GetTempPath"/>, <c>aspire-sqlite-{name}-{random}</c>, unique to this AppHost run and resource.
    /// Adding the resource has no file system side effect. When the application starts (the <see cref="BeforeStartEvent"/>, never when
    /// publishing), the directory is created, accessible only by the current user on Unix; SQLite creates the database file on first use.
    /// When the AppHost stops (<see cref="IHostApplicationLifetime.ApplicationStopped"/>), the directory and everything in it are deleted.
    /// Deletion is best effort: a failure is logged as a warning, and a crashed or killed AppHost leaves the directory behind in the
    /// temp folder, where it can be deleted by hand.
    /// </para>
    /// <para>
    /// The resource has a health check named <c>{name}-file</c>; a dependent resource that uses
    /// <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/> waits until the
    /// directory exists and the file, if present, is readable. The check does not test writability, because that cannot be done
    /// without writing; a read-only location surfaces as a SQLite error on first use.
    /// </para>
    /// </remarks>
    public static IResourceBuilder<SqliteResource> AddSqlite(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var ephemeralDirectory = Path.Combine(Path.GetTempPath(), $"aspire-sqlite-{name}-{Guid.NewGuid():N}");
        var resource = new SqliteResource(name, ephemeralDirectory);

        if (!builder.ExecutionContext.IsPublishMode)
        {
            builder.Eventing.Subscribe<BeforeStartEvent>((@event, _) =>
            {
                var directory = resource.DataDirectory;
                if (!string.Equals(directory, ephemeralDirectory, StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(directory);
                    return Task.CompletedTask;
                }

                if (OperatingSystem.IsWindows())
                {
                    Directory.CreateDirectory(directory);
                }
                else
                {
                    Directory.CreateDirectory(directory, OwnerOnly);
                }

                var logger = @event.Services.GetService<ILoggerFactory>()?.CreateLogger(typeof(SqliteResourceExtensions)) ?? NullLogger.Instance;
                @event.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.Register(() => DeleteEphemeralDirectory(directory, logger));
                return Task.CompletedTask;
            });
        }

        var healthCheckName = $"{name}-file";
        builder.Services.AddHealthChecks().AddCheck(healthCheckName, new SqliteFileHealthCheck(resource));

        return builder.AddResource(resource)
            .WithDashboardSnapshot()
            .WithHealthCheck(healthCheckName);
    }

    /// <summary>
    /// Keeps the database in a host folder across runs: the database file becomes <c>{folder}/{name}.db</c>, and the folder is never deleted.
    /// </summary>
    /// <param name="builder">The SQLite resource.</param>
    /// <param name="source">The host folder; a relative path is resolved against the AppHost directory.</param>
    /// <returns>The resource builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <remarks>
    /// The name mirrors the container helpers' <c>WithDataBindMount</c>: the data lives in a host folder that the developer owns. The folder
    /// is created when the application starts (never when publishing) and is not deleted when it stops; the temporary directory is then
    /// never created. Connection strings, the health check and the dashboard follow the new location, even for references added before
    /// this call.
    /// </remarks>
    public static IResourceBuilder<SqliteResource> WithDataBindMount(this IResourceBuilder<SqliteResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        builder.Resource.DataDirectory = Path.GetFullPath(source, builder.ApplicationBuilder.AppHostDirectory);
        return builder.WithDashboardSnapshot();
    }

    /// <summary>
    /// Keeps the database in the host folder that an AppHost setting names, as <see cref="WithDataBindMount"/> does; without the setting, or
    /// with a blank one, the database stays ephemeral.
    /// </summary>
    /// <param name="builder">The SQLite resource.</param>
    /// <param name="configurationKey">The AppHost configuration key, such as <c>AppHost:DataFolder</c>; a relative folder is resolved against the AppHost directory.</param>
    /// <returns>The resource builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="configurationKey"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <remarks>
    /// The setting is read when this is called, from the AppHost configuration (user secrets, <c>appsettings</c>, environment variables or the
    /// command line, such as <c>--AppHost:DataFolder=./data</c>). Ephemeral by default keeps test runs and fresh clones isolated; a developer
    /// opts in to keeping data.
    /// </remarks>
    public static IResourceBuilder<SqliteResource> WithDataBindMountFromConfiguration(this IResourceBuilder<SqliteResource> builder, string configurationKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);

        var folder = builder.ApplicationBuilder.Configuration[configurationKey];
        return string.IsNullOrWhiteSpace(folder) ? builder : builder.WithDataBindMount(folder);
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
    /// schema the owner creates. The path is read when the environment is built, so a later <c>WithDataBindMount</c> is honored.
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

        var resource = database.Resource;
        var variable = $"ConnectionStrings__{connectionName ?? resource.Name}";
        return builder
            .WithEnvironment(context => context.EnvironmentVariables[variable] = new DbConnectionStringBuilder
            {
                ["Data Source"] = resource.DatabasePath,
                ["Mode"] = "ReadOnly",
            }.ConnectionString)
            .WithRelationship(resource, "Reference");
    }

    private static IResourceBuilder<SqliteResource> WithDashboardSnapshot(this IResourceBuilder<SqliteResource> builder) =>
        builder.WithInitialState(new CustomResourceSnapshot
        {
            ResourceType = "Sqlite",
            State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success),
            Properties = [new ResourcePropertySnapshot("DatabasePath", builder.Resource.DatabasePath)],
        });

    private static void DeleteEphemeralDirectory(string directory, ILogger logger)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SqliteLog.EphemeralDirectoryNotDeleted(logger, directory, ex);
        }
    }
}
