using System.Security.Cryptography;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Verifies the archive client against disposable leases and a deterministic native process seam.</summary>
/// <param name="fixture">The shared PostgreSQL test server fixture.</param>
public sealed class PostgreSqlTestArchiveClientTests(PostgreSqlTestServerFixture fixture)
{
    [Fact]
    public async Task Capturing_and_restoring_preserves_exact_archive_bytes_and_uses_an_atomic_target_command()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var source = await fixture.CreateDatabase(ct);
        await using var target = await fixture.CreateDatabase(ct);
        await using var workspace = TestWorkspace.Create("postgresql-archive");
        byte[] original = [0x50, 0x47, 0, 0xff];
        var process = new PostgreSqlTestArchiveProcessFixture(original);
        var client = new PostgreSqlTestArchiveClient(process.Start, 64, process.Run);

        // Act
        var archive = await client.Capture(source, workspace, ct);
        await client.Restore(archive, target, ct);
        var stored = await File.ReadAllBytesAsync(archive.Path, ct);

        // Assert
        stored.ShouldBe(original);
        archive.Length.ShouldBe(original.LongLength);
        archive.Sha256.ShouldBe(Convert.ToHexString(SHA256.HashData(original)));
        archive.TableOfContents.ShouldBe("public fixture table\n");
        process.RestoreInput.ToArray().ShouldBe(original);
        process.Commands.ShouldHaveCount(3);
        process.Commands[0].ArgumentList.ToArray().ShouldBe(
            ["pg_dump", "--format=custom", "--no-password", $"--dbname={source.DatabaseName}"]);
        process.Commands[1].ArgumentList.ToArray().ShouldBe(["pg_restore", "--list"]);
        process.Commands[2].ArgumentList.ToArray().ShouldBe(
            ["pg_restore", "--no-password", "--single-transaction", "--exit-on-error", $"--dbname={target.DatabaseName}"]);
    }

    [Fact]
    public async Task Replacing_the_archive_with_different_bytes_refuses_restore_before_creating_a_target_command()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var source = await fixture.CreateDatabase(ct);
        await using var target = await fixture.CreateDatabase(ct);
        await using var workspace = TestWorkspace.Create("postgresql-archive");
        var process = new PostgreSqlTestArchiveProcessFixture([1, 2, 3]);
        var client = new PostgreSqlTestArchiveClient(process.Start, 64, process.Run);
        var archive = await client.Capture(source, workspace, ct);
        await File.WriteAllBytesAsync(archive.Path, new byte[] { 1, 2, 4 }, ct);

        // Act
        Func<Task> restore = () => client.Restore(archive, target, ct);
        var failure = await restore.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldContain("digest", StringComparison.OrdinalIgnoreCase);
        process.Commands.ShouldHaveCount(2);
        process.RestoreInput.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Refusing_the_source_database_leaves_the_existing_archive_intact()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var source = await fixture.CreateDatabase(ct);
        await using var workspace = TestWorkspace.Create("postgresql-archive");
        var process = new PostgreSqlTestArchiveProcessFixture([1, 2, 3]);
        var client = new PostgreSqlTestArchiveClient(process.Start, 64, process.Run);
        var captured = await client.Capture(source, workspace, ct);
        var archive = new PostgreSqlTestArchive(
            captured.Path, captured.Sha256, captured.Length, captured.TableOfContents, source.DatabaseName);

        // Act
        Func<Task> restore = () => client.Restore(archive, source, ct);
        _ = await restore.ShouldThrow<ArgumentException>();

        // Assert
        process.Commands.ShouldHaveCount(2);
        File.Exists(archive.Path).ShouldBeTrue();
    }

    [Fact]
    public async Task An_oversized_dump_discards_its_partial_file_and_does_not_start_the_toc_command()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var source = await fixture.CreateDatabase(ct);
        await using var workspace = TestWorkspace.Create("postgresql-archive");
        var process = new PostgreSqlTestArchiveProcessFixture([0x50, 0x47, 0, 0xff]);
        var client = new PostgreSqlTestArchiveClient(process.Start, 3, process.Run);

        // Act
        Func<Task> capture = () => client.Capture(source, workspace, ct);
        _ = await capture.ShouldThrow<InvalidOperationException>();
        var remaining = Directory.EnumerateFileSystemEntries(workspace.Root).ToArray();

        // Assert
        process.DumpLimit.ShouldBe(3);
        process.Commands.ShouldHaveCount(1);
        remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_restore_reports_the_command_and_exit_code_without_echoing_authentication_or_archive_bytes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var source = await fixture.CreateDatabase(ct);
        await using var target = await fixture.CreateDatabase(ct);
        await using var workspace = TestWorkspace.Create("postgresql-archive");
        var process = new PostgreSqlTestArchiveProcessFixture("synthetic-content"u8.ToArray()) { RestoreExitCode = 17 };
        var client = new PostgreSqlTestArchiveClient(process.Start, 64, process.Run);
        var archive = await client.Capture(source, workspace, ct);

        // Act
        Func<Task> restore = () => client.Restore(archive, target, ct);
        var failure = await restore.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("pg_restore", StringComparison.Ordinal);
        failure.Message.ShouldContain("17", StringComparison.Ordinal);
        failure.ToString().ShouldNotContain(PostgreSqlTestArchiveProcessFixture.Secret, StringComparison.Ordinal);
        failure.ToString().ShouldNotContain("synthetic-content", StringComparison.Ordinal);
        File.Exists(archive.Path).ShouldBeTrue();
    }
}
