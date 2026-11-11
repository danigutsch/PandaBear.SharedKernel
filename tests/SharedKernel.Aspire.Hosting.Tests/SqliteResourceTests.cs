using System.Data.Common;
using System.Runtime.Versioning;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests the SQLite resource with a persistent data folder (<c>WithDataBindMount</c>): path resolution, directory creation at start, connection string, dashboard snapshot, health check and consumer references.</summary>
public sealed class SqliteResourceTests
{
    /// <summary>Verifies that a relative folder is resolved against the AppHost directory and the file is named after the resource.</summary>
    [Fact]
    public void WithDataBindMount_resolves_a_relative_folder_against_the_app_host_directory()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        var relative = Path.GetRelativePath(builder.AppHostDirectory, temp.Path);
        Path.IsPathRooted(relative).ShouldBeFalse();

        // Act
        var sqlite = builder.AddSqlite("db").WithDataBindMount(relative);

        // Assert
        sqlite.Resource.DataDirectory.ShouldBe(temp.Path);
        sqlite.Resource.DatabasePath.ShouldBe(Path.Combine(temp.Path, "db.db"));
    }

    /// <summary>Verifies that an absolute folder is kept as given.</summary>
    [Fact]
    public void WithDataBindMount_keeps_an_absolute_folder()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var sqlite = builder.AddSqlite("app").WithDataBindMount(temp.Path);

        // Assert
        sqlite.Resource.DataDirectory.ShouldBe(temp.Path);
        sqlite.Resource.DatabasePath.ShouldBe(Path.Combine(temp.Path, "app.db"));
    }

    /// <summary>Verifies that configuring the folder has no file system side effect, so model building and manifest generation stay pure.</summary>
    [Fact]
    public void WithDataBindMount_does_not_create_the_directory()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        builder.AddSqlite("db").WithDataBindMount(Path.Combine(temp.Path, "nested"));

        // Assert
        Directory.Exists(temp.Path).ShouldBeFalse();
    }

    /// <summary>Verifies that the before-start event creates the folder but not the database file, and the temporary folder is never created.</summary>
    [Fact]
    public async Task BeforeStart_creates_the_folder_but_not_the_database_file()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var folder = Path.Combine(temp.Path, "nested");
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db");
        var ephemeral = sqlite.Resource.DataDirectory;
        sqlite.WithDataBindMount(folder);

        // Act
        await using var run = await AppHostRun.Start(builder);

        // Assert
        Directory.Exists(folder).ShouldBeTrue();
        File.Exists(Path.Combine(folder, "db.db")).ShouldBeFalse();
        Directory.Exists(ephemeral).ShouldBeFalse();
    }

    /// <summary>Verifies that stopping the AppHost never deletes a persistent folder or its files.</summary>
    [Fact]
    public async Task Stop_keeps_the_persistent_folder_and_its_files()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(temp.Path);
        await using var run = await AppHostRun.Start(builder);
        await File.WriteAllTextAsync(sqlite.Resource.DatabasePath, "x", TestContext.Current.CancellationToken);

        // Act
        run.Stop();

        // Assert
        File.Exists(sqlite.Resource.DatabasePath).ShouldBeTrue();
    }

    /// <summary>Verifies that the before-start event creates nothing when the application is published.</summary>
    [Fact]
    public async Task BeforeStart_does_not_create_the_folder_in_publish_mode()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var folder = Path.Combine(temp.Path, "nested");
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = ["--publisher", "manifest", "--output-path", Path.Combine(temp.Path, "out")], DisableDashboard = true });
        builder.AddSqlite("db").WithDataBindMount(folder);

        // Act
        await using var run = await AppHostRun.Start(builder);

        // Assert
        Directory.Exists(folder).ShouldBeFalse();
    }

    /// <summary>Verifies the connection string for a plain path.</summary>
    [Fact]
    public async Task ConnectionStringExpression_uses_the_absolute_path_as_data_source()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(temp.Path);

        // Act
        var value = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe($"Data Source={Path.Combine(temp.Path, "db.db")}");
    }

    /// <summary>Verifies that a path with spaces survives parsing of the connection string.</summary>
    [Fact]
    public async Task ConnectionStringExpression_round_trips_a_path_with_spaces()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var folder = Path.Combine(temp.Path, "my data");
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(folder);

        // Act
        var value = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken);

        // Assert
        new DbConnectionStringBuilder { ConnectionString = value }["Data Source"].ShouldBe(Path.Combine(folder, "db.db"));
    }

    /// <summary>Verifies that a semicolon in the path is quoted so the connection string stays one segment.</summary>
    [Fact]
    public async Task ConnectionStringExpression_quotes_a_path_with_a_semicolon()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "a;b", "db.db");
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(Path.Combine(temp.Path, "a;b"));

        // Act
        var value = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe($"Data Source=\"{path}\"");
        new DbConnectionStringBuilder { ConnectionString = value }["Data Source"].ShouldBe(path);
    }

    /// <summary>Verifies the initial dashboard snapshot with the database path, updated by <c>WithDataBindMount</c>.</summary>
    [Fact]
    public void Dashboard_snapshot_shows_the_persistent_database_path()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var sqlite = builder.AddSqlite("db").WithDataBindMount(temp.Path);

        // Assert
        var snapshot = sqlite.Resource.Annotations.OfType<ResourceSnapshotAnnotation>().ShouldHaveSingleItem().InitialSnapshot;
        snapshot.ResourceType.ShouldBe("Sqlite");
        snapshot.State!.Text.ShouldBe(KnownResourceStates.Running);
        snapshot.State.Style.ShouldBe(KnownResourceStateStyles.Success);
        var property = snapshot.Properties.ShouldHaveSingleItem();
        property.Name.ShouldBe("DatabasePath");
        property.Value.ShouldBe(Path.Combine(temp.Path, "db.db"));
    }

    /// <summary>Verifies the health check annotation key.</summary>
    [Fact]
    public void AddSqlite_adds_a_file_health_check()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var sqlite = builder.AddSqlite("db");

        // Assert
        sqlite.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldHaveSingleItem().Key.ShouldBe("db-file");
    }

    /// <summary>Verifies the health check follows the persistent folder and is healthy without a database file.</summary>
    [Fact]
    public async Task Health_check_is_healthy_when_the_folder_exists_and_the_file_is_missing()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(temp.Path);
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db").WithDataBindMount(temp.Path);

        // Act
        var result = await HealthCheckRunner.Run(builder, "db-file");

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Verifies the health check is healthy for an existing folder with a readable database file.</summary>
    [Fact]
    public async Task Health_check_is_healthy_when_the_file_exists_and_is_readable()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(temp.Path);
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(temp.Path);
        await File.WriteAllTextAsync(sqlite.Resource.DatabasePath, "x", TestContext.Current.CancellationToken);

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
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(temp.Path);
        await File.WriteAllTextAsync(sqlite.Resource.DatabasePath, "x", TestContext.Current.CancellationToken);
        var before = Directory.EnumerateFileSystemEntries(temp.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();

        // Act
        await HealthCheckRunner.Run(builder, "db-file");
        await HealthCheckRunner.Run(builder, "db-file");

        // Assert
        Directory.EnumerateFileSystemEntries(temp.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray().ShouldBe(before);
    }

    /// <summary>Verifies writability is not probed: a read-only folder is still healthy.</summary>
    [Fact(SkipType = typeof(ReadOnlyDirectory), SkipUnless = nameof(ReadOnlyDirectory.IsEnforced), Skip = "Directory permissions are not enforced for this user or host.")]
    [UnsupportedOSPlatform("windows")]
    public async Task Health_check_does_not_probe_writability()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(temp.Path);
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db").WithDataBindMount(temp.Path);
        using var readOnly = new ReadOnlyDirectory(temp.Path);

        // Act
        var result = await HealthCheckRunner.Run(builder, "db-file");

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Verifies the health check is unhealthy and does not reveal the path when the folder is gone.</summary>
    [Fact]
    public async Task Health_check_is_unhealthy_when_the_folder_is_missing()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db").WithDataBindMount(Path.Combine(temp.Path, "gone"));

        // Act
        var result = await HealthCheckRunner.Run(builder, "db-file");

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.Contains(temp.Path, StringComparison.Ordinal).ShouldBeFalse();
    }

    /// <summary>Verifies that consumers referenced before <c>WithDataBindMount</c> still receive the persistent path.</summary>
    [Fact]
    public async Task WithDataBindMount_after_a_reference_still_reaches_the_consumer()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db");
        var consumer = builder.AddContainer("consumer", "example/image").WithReference(sqlite, connectionName: "Default");
        var reader = builder.AddContainer("public", "example/image").WithReadOnlyReference(sqlite, connectionName: "Default");

        // Act
        sqlite.WithDataBindMount(temp.Path);
        var variables = await EnvironmentVariables.Of(consumer.Resource);
        var readerVariables = await EnvironmentVariables.Of(reader.Resource);

        // Assert
        var path = Path.Combine(temp.Path, "db.db");
        variables["ConnectionStrings__Default"].ShouldBe($"Data Source={path}");
        readerVariables["ConnectionStrings__Default"].ShouldBe($"Data Source={path};Mode=ReadOnly");
    }

    /// <summary>Verifies the argument validation of <c>AddSqlite</c>.</summary>
    [Fact]
    public void AddSqlite_validates_its_arguments()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var nullBuilder = () => SqliteResourceExtensions.AddSqlite(null!, "db");
        var blankName = () => builder.AddSqlite(" ");

        // Assert
        nullBuilder.ShouldThrow<ArgumentNullException>();
        blankName.ShouldThrow<ArgumentException>();
    }

    /// <summary>Verifies the argument validation of <c>WithDataBindMount</c>.</summary>
    [Fact]
    public void WithDataBindMount_validates_its_arguments()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db");

        // Act
        var nullResource = () => SqliteResourceExtensions.WithDataBindMount(null!, "/tmp/data");
        var blankFolder = () => sqlite.WithDataBindMount(" ");

        // Assert
        nullResource.ShouldThrow<ArgumentNullException>();
        blankFolder.ShouldThrow<ArgumentException>();
    }
}
