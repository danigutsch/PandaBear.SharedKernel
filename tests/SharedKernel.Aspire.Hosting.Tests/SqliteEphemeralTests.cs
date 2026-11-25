using System.Runtime.Versioning;
using Aspire.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharedKernel.Aspire.Hosting.Tests;

/// <summary>Tests the default, ephemeral SQLite database: a fresh temporary directory per run that is deleted when the AppHost stops.</summary>
public sealed class SqliteEphemeralTests
{
    /// <summary>Gets a value indicating whether Unix file modes apply, for <c>SkipUnless</c> conditions.</summary>
    public static bool IsUnix => !OperatingSystem.IsWindows();

    /// <summary>Verifies that the database lives in a directory under the temp path, named after the resource.</summary>
    [Fact]
    public void AddSqlite_without_a_data_directory_places_the_database_under_the_temp_path()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var sqlite = builder.AddSqlite("app-db");

        // Assert
        Path.GetDirectoryName(sqlite.Resource.DataDirectory).ShouldBe(Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
        Path.GetFileName(sqlite.Resource.DataDirectory).ShouldStartWith("aspire-sqlite-app-db-", StringComparison.Ordinal);
        sqlite.Resource.DatabasePath.ShouldBe(Path.Combine(sqlite.Resource.DataDirectory, "app-db.db"));
    }

    /// <summary>Verifies that every builder, that is every AppHost run, gets its own directory.</summary>
    [Fact]
    public void AddSqlite_without_a_data_directory_is_unique_per_run()
    {
        // Arrange
        var first = DistributedApplication.CreateBuilder([]);
        var second = DistributedApplication.CreateBuilder([]);

        // Act
        var a = first.AddSqlite("db");
        var b = second.AddSqlite("db");

        // Assert
        a.Resource.DataDirectory.ShouldNotBe(b.Resource.DataDirectory);
    }

    /// <summary>Verifies that two resources of one run get different directories.</summary>
    [Fact]
    public void AddSqlite_without_a_data_directory_is_unique_per_resource()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var a = builder.AddSqlite("one");
        var b = builder.AddSqlite("two");

        // Assert
        a.Resource.DataDirectory.ShouldNotBe(b.Resource.DataDirectory);
    }

    /// <summary>Verifies that adding the resource does not create the directory.</summary>
    [Fact]
    public void AddSqlite_without_a_data_directory_has_no_file_system_side_effect()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var sqlite = builder.AddSqlite("db");

        // Assert
        Directory.Exists(sqlite.Resource.DataDirectory).ShouldBeFalse();
    }

    /// <summary>Verifies that starting creates the directory but not the database file, and stopping deletes the directory with its contents.</summary>
    [Fact]
    public async Task Ephemeral_directory_exists_after_start_and_is_deleted_after_stop()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db");

        // Act
        await using var run = await AppHostRun.Start(builder);
        var existedAfterStart = Directory.Exists(sqlite.Resource.DataDirectory);
        var databaseCreated = File.Exists(sqlite.Resource.DatabasePath);
        Directory.CreateDirectory(Path.Combine(sqlite.Resource.DataDirectory, "media"));
        await File.WriteAllTextAsync(sqlite.Resource.DatabasePath, "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sqlite.Resource.DataDirectory, "media", "a.png"), "x", TestContext.Current.CancellationToken);
        run.Stop();

        // Assert
        existedAfterStart.ShouldBeTrue();
        databaseCreated.ShouldBeFalse();
        Directory.Exists(sqlite.Resource.DataDirectory).ShouldBeFalse();
    }

    /// <summary>Verifies that the temporary directory, which holds application data, is private to the current user.</summary>
    [Fact(SkipUnless = nameof(IsUnix), Skip = "Unix file modes do not apply on Windows.")]
    [UnsupportedOSPlatform("windows")]
    public async Task Ephemeral_directory_is_accessible_only_by_the_current_user()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db");

        // Act
        await using var run = await AppHostRun.Start(builder);
        var mode = File.GetUnixFileMode(sqlite.Resource.DataDirectory);
        run.Stop();

        // Assert
        mode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Verifies that publishing neither creates nor deletes anything.</summary>
    [Fact]
    public async Task Ephemeral_database_has_no_file_system_side_effect_in_publish_mode()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = ["--publisher", "manifest", "--output-path", Path.Combine(temp.Path, "out")], DisableDashboard = true });
        var sqlite = builder.AddSqlite("db");

        // Act
        await using var run = await AppHostRun.Start(builder);
        var existedAfterStart = Directory.Exists(sqlite.Resource.DataDirectory);
        run.Stop();

        // Assert
        existedAfterStart.ShouldBeFalse();
        Directory.EnumerateFileSystemEntries(temp.Path).ShouldBeEmpty();
    }

    /// <summary>Verifies that the connection string points at the database file in the temporary directory.</summary>
    [Fact]
    public async Task Ephemeral_connection_string_uses_the_temporary_database_path()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db");
        var consumer = builder.AddContainer("consumer", "example/image").WithReference(sqlite, connectionName: "Default");

        // Act
        var expression = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken);
        var variables = await EnvironmentVariables.Of(consumer.Resource);

        // Assert
        expression.ShouldBe($"Data Source={sqlite.Resource.DatabasePath}");
        variables["ConnectionStrings__Default"].ShouldBe($"Data Source={sqlite.Resource.DatabasePath}");
    }

    /// <summary>Verifies that a read-only consumer receives the temporary database path.</summary>
    [Fact]
    public async Task Ephemeral_read_only_reference_uses_the_temporary_database_path()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db");
        var consumer = builder.AddContainer("public", "example/image").WithReadOnlyReference(sqlite, connectionName: "Default");

        // Act
        var variables = await EnvironmentVariables.Of(consumer.Resource);

        // Assert
        variables["ConnectionStrings__Default"].ShouldBe($"Data Source={sqlite.Resource.DatabasePath};Mode=ReadOnly");
    }

    /// <summary>Verifies that the health check is healthy once the run has created the directory.</summary>
    [Fact]
    public async Task Ephemeral_health_check_is_healthy_after_start()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        builder.AddSqlite("db");
        await using var run = await AppHostRun.Start(builder);

        // Act
        var result = await HealthCheckRunner.Run(builder, "db-file");
        run.Stop();

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Verifies that the data directory can feed a consumer's setting through a callback, for files that live next to the database.</summary>
    [Fact]
    public async Task DataDirectory_can_feed_a_consumer_setting()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db");
        var consumer = builder.AddContainer("web", "example/image")
            .WithEnvironment(context => context.EnvironmentVariables["Media__Path"] = Path.Combine(sqlite.Resource.DataDirectory, "media"));

        // Act
        var variables = await EnvironmentVariables.Of(consumer.Resource);

        // Assert
        variables["Media__Path"].ShouldBe(Path.Combine(sqlite.Resource.DataDirectory, "media"));
    }
}
