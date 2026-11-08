using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Runtime.Versioning;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests the SQLite database file resource builder: path resolution, directory creation at start, connection string, dashboard snapshot, health check and consumer references.</summary>
public sealed class SqliteResourceTests
{
    /// <summary>Verifies that a relative path is resolved against the AppHost directory.</summary>
    [Fact]
    public void AddSqlite_resolves_a_relative_path_against_the_app_host_directory()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        var relative = Path.GetRelativePath(builder.AppHostDirectory, Path.Combine(temp.Path, "app.db"));
        Path.IsPathRooted(relative).ShouldBeFalse();

        // Act
        var sqlite = builder.AddSqlite("db", relative);

        // Assert
        sqlite.Resource.DatabasePath.ShouldBe(Path.Combine(temp.Path, "app.db"));
    }

    /// <summary>Verifies that an absolute path is kept as given.</summary>
    [Fact]
    public void AddSqlite_keeps_an_absolute_path()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "app.db");
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var sqlite = builder.AddSqlite("db", path);

        // Assert
        sqlite.Resource.DatabasePath.ShouldBe(path);
    }

    /// <summary>Verifies that adding the resource has no file system side effect, so model building and manifest generation stay pure.</summary>
    [Fact]
    public void AddSqlite_does_not_create_the_directory()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "nested", "app.db");
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        builder.AddSqlite("db", path);

        // Assert
        Directory.Exists(temp.Path).ShouldBeFalse();
    }

    /// <summary>Verifies that the before-start event creates the parent directory but not the database file.</summary>
    [Fact]
    public async Task BeforeStart_creates_the_parent_directory_but_not_the_database_file()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "nested", "app.db");
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db", path);

        // Act
        await BeforeStartPublisher.Publish(builder);

        // Assert
        Directory.Exists(Path.Combine(temp.Path, "nested")).ShouldBeTrue();
        File.Exists(path).ShouldBeFalse();
    }

    /// <summary>Verifies that the before-start event creates nothing when the application is published.</summary>
    [Fact]
    public async Task BeforeStart_does_not_create_the_directory_in_publish_mode()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "nested", "app.db");
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = ["--publisher", "manifest", "--output-path", Path.Combine(temp.Path, "out")], DisableDashboard = true });
        builder.AddSqlite("db", path);

        // Act
        await BeforeStartPublisher.Publish(builder);

        // Assert
        Directory.Exists(Path.Combine(temp.Path, "nested")).ShouldBeFalse();
    }

    /// <summary>Verifies the connection string for a plain path.</summary>
    [Fact]
    public async Task ConnectionStringExpression_uses_the_absolute_path_as_data_source()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "app.db");
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db", path);

        // Act
        var value = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe($"Data Source={path}");
    }

    /// <summary>Verifies that a path with spaces survives parsing of the connection string.</summary>
    [Fact]
    public async Task ConnectionStringExpression_round_trips_a_path_with_spaces()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "my data", "app file.db");
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db", path);

        // Act
        var value = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken);

        // Assert
        new System.Data.Common.DbConnectionStringBuilder { ConnectionString = value }["Data Source"].ShouldBe(path);
    }

    /// <summary>Verifies that a semicolon in the path is quoted so the connection string stays one segment.</summary>
    [Fact]
    public async Task ConnectionStringExpression_quotes_a_path_with_a_semicolon()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "a;b", "app.db");
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db", path);

        // Act
        var value = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe($"Data Source=\"{path}\"");
        var parsed = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = value };
        parsed["Data Source"].ShouldBe(path);
    }

    /// <summary>Verifies the initial dashboard snapshot with the database path.</summary>
    [Fact]
    public void AddSqlite_publishes_an_initial_dashboard_snapshot_with_the_database_path()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "app.db");
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var sqlite = builder.AddSqlite("db", path);

        // Assert
        var snapshot = sqlite.Resource.Annotations.OfType<ResourceSnapshotAnnotation>().ShouldHaveSingleItem().InitialSnapshot;
        snapshot.ResourceType.ShouldBe("Sqlite");
        snapshot.State!.Text.ShouldBe(KnownResourceStates.Running);
        var property = snapshot.Properties.ShouldHaveSingleItem();
        property.Name.ShouldBe("DatabasePath");
        property.Value.ShouldBe(path);
        snapshot.State.Style.ShouldBe(KnownResourceStateStyles.Success);
    }

    /// <summary>Verifies the health check annotation key.</summary>
    [Fact]
    public void AddSqlite_adds_a_file_health_check()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var sqlite = builder.AddSqlite("db", Path.Combine(temp.Path, "app.db"));

        // Assert
        sqlite.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem().Key.ShouldBe("db-file");
    }

    /// <summary>Verifies the health check is healthy for an existing directory without a database file.</summary>
    [Fact]
    public async Task Health_check_is_healthy_when_the_directory_exists_and_the_file_is_missing()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(temp.Path);
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db", Path.Combine(temp.Path, "app.db"));

        // Act
        var result = await HealthCheckRunner.Run(builder, "db-file");

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Verifies the health check is healthy for an existing directory with a readable database file.</summary>
    [Fact]
    public async Task Health_check_is_healthy_when_the_file_exists_and_is_readable()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(temp.Path);
        var path = Path.Combine(temp.Path, "app.db");
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db", path);
        await File.WriteAllTextAsync(path, "x", TestContext.Current.CancellationToken);

        // Act
        var result = await HealthCheckRunner.Run(builder, "db-file");

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Verifies running the health check neither creates nor leaves behind any file or directory.</summary>
    [Fact]
    public async Task Health_check_does_not_create_or_leave_files_behind()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(temp.Path);
        var path = Path.Combine(temp.Path, "app.db");
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db", path);
        await File.WriteAllTextAsync(path, "x", TestContext.Current.CancellationToken);
        var before = Directory.EnumerateFileSystemEntries(temp.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();

        // Act
        await HealthCheckRunner.Run(builder, "db-file");
        await HealthCheckRunner.Run(builder, "db-file");

        // Assert
        Directory.EnumerateFileSystemEntries(temp.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray().ShouldBe(before);
    }

    /// <summary>Verifies writability is not probed: a read-only directory is still healthy.</summary>
    [Fact(SkipType = typeof(ReadOnlyDirectory), SkipUnless = nameof(ReadOnlyDirectory.IsEnforced), Skip = "Directory permissions are not enforced for this user or host.")]
    [UnsupportedOSPlatform("windows")]
    public async Task Health_check_does_not_probe_writability()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(temp.Path);
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db", Path.Combine(temp.Path, "app.db"));
        using var readOnly = new ReadOnlyDirectory(temp.Path);

        // Act
        var result = await HealthCheckRunner.Run(builder, "db-file");

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Verifies the health check is unhealthy and does not reveal the path when the directory is gone.</summary>
    [Fact]
    public async Task Health_check_is_unhealthy_when_the_directory_is_missing()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "gone", "app.db");
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db", path);
        Directory.CreateDirectory(Path.Combine(temp.Path, "gone"));
        Directory.Delete(Path.Combine(temp.Path, "gone"));

        // Act
        var result = await HealthCheckRunner.Run(builder, "db-file");

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.Contains(temp.Path, StringComparison.Ordinal).ShouldBeFalse();
    }

    /// <summary>Verifies that referencing the resource with a connection name injects the connection string variable.</summary>
    [Fact]
    public async Task WithReference_with_a_connection_name_injects_the_connection_string()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "app.db");
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db", path);
        var consumer = builder.AddContainer("consumer", "example/image").WithReference(sqlite, connectionName: "Default");

        // Act
        var variables = await EnvironmentVariables.Of(consumer.Resource);

        // Assert
        variables["ConnectionStrings__Default"].ShouldBe($"Data Source={path}");
    }

    /// <summary>Verifies the argument validation of <c>AddSqlite</c>.</summary>
    [Fact]
    public void AddSqlite_validates_its_arguments()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var nullBuilder = () => SqliteResourceExtensions.AddSqlite(null!, "db", "/tmp/app.db");
        var blankName = () => builder.AddSqlite(" ", "/tmp/app.db");
        var blankPath = () => builder.AddSqlite("db", " ");

        // Assert
        nullBuilder.ShouldThrow<ArgumentNullException>();
        blankName.ShouldThrow<ArgumentException>();
        blankPath.ShouldThrow<ArgumentException>();
    }
}
