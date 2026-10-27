using System.Text;
using PandaBear.Npgsql.Testing.Host;

namespace PandaBear.Npgsql.Tests;

/// <summary>Tests resolving and publishing the ready file through which the owner host announces its server's connection string. Each test uses its own temporary directory, removed on disposal.</summary>
public sealed class ReadyFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("sk-ready-file-").FullName;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public void An_unset_or_blank_variable_is_refused_before_anything_starts(string? value)
    {
        // Arrange
        Func<object?> resolve = () => ReadyFile.Resolve(FakeEnvironment.WithReadyFile(value));

        // Act
        var failure = resolve.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(PostgreSqlTestServer.ReadyFileVariable, "the message must tell the reader which variable to set");
    }

    [Fact]
    public void A_relative_path_is_refused_because_the_owner_cannot_know_what_it_is_relative_to()
    {
        // Arrange
        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(_directory, "ready.txt"));
        Func<object?> resolve = () => ReadyFile.Resolve(FakeEnvironment.WithReadyFile(relativePath));

        // Act
        var failure = resolve.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(PostgreSqlTestServer.ReadyFileVariable, "the message must tell the reader which variable to fix");
    }

    [Fact]
    public void A_missing_directory_is_refused_before_a_container_is_started_for_nothing()
    {
        // Arrange
        Func<object?> resolve = () => ReadyFile.Resolve(FakeEnvironment.WithReadyFile(Path.Combine(_directory, "missing", "ready.txt")));

        // Act
        var failure = resolve.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(PostgreSqlTestServer.ReadyFileVariable, "the message must tell the reader which variable to fix");
    }

    [Fact]
    public void An_existing_file_is_refused_because_the_owner_would_read_it_as_ready_before_the_server_is()
    {
        // Arrange
        var path = Path.Combine(_directory, "ready.txt");
        File.WriteAllText(path, "Host=stale;Password=stale-secret");
        Func<object?> resolve = () => ReadyFile.Resolve(FakeEnvironment.WithReadyFile(path));

        // Act
        var failure = resolve.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(PostgreSqlTestServer.ReadyFileVariable, "the message must tell the reader which variable to fix");
        File.ReadAllText(path).ShouldBe("Host=stale;Password=stale-secret", "a refused file is left alone");
    }

    [Fact]
    public void A_new_file_in_an_existing_directory_resolves_to_its_path_without_surrounding_whitespace()
    {
        // Arrange
        var path = Path.Combine(_directory, "ready.txt");

        // Act
        var resolved = ReadyFile.Resolve(FakeEnvironment.WithReadyFile($"  {path}\n"));

        // Assert
        resolved.ShouldBe(path);
        File.Exists(path).ShouldBeFalse("resolving the location must not create the file, which would announce a server that is not there");
    }

    [Fact]
    public void The_connection_string_is_published_as_one_utf8_line_without_a_byte_order_mark()
    {
        // Arrange
        var path = Path.Combine(_directory, "ready.txt");
        const string connectionString = "Host=127.0.0.1;Port=41234;Username=postgres;Password=\"p\u00e4ssw\u00f6rd\"";

        // Act
        ReadyFile.Publish(path, connectionString);
        var content = File.ReadAllBytes(path);

        // Assert
        content.ShouldBe(Encoding.UTF8.GetBytes(connectionString + "\n"), "the owner reads the first line; a byte order mark would become part of the Host keyword");
    }

    [Fact]
    public void Publishing_replaces_the_file_in_one_step_and_leaves_no_temporary_file()
    {
        // Arrange
        var path = Path.Combine(_directory, "ready.txt");
        ReadyFile.Publish(path, "Host=first");

        // Act
        ReadyFile.Publish(path, "Host=second");
        var files = Directory.GetFiles(_directory).Select(Path.GetFileName).ToArray();

        // Assert
        File.ReadAllText(path).ShouldBe("Host=second\n");
        files.ShouldBe(["ready.txt"], "the line is written to a temporary file first and moved over the ready file, so the owner never reads a partial line");
    }

    [Fact]
    public void Nothing_appears_at_the_ready_path_until_the_whole_line_is_written()
    {
        // Arrange
        var path = Path.Combine(_directory, "ready.txt");

        // The temporary file cannot be created, which stands for a write that fails half way.
        _ = Directory.CreateDirectory(path + ".tmp");
        Action publish = () => ReadyFile.Publish(path, "Host=127.0.0.1;Password=owner-secret");

        // Act
        _ = publish.ShouldThrow<UnauthorizedAccessException>();

        // Assert
        File.Exists(path).ShouldBeFalse("the owner treats the existence of the ready file as the signal that the line is complete");
    }

    [Fact]
    public void The_published_file_is_readable_by_its_owner_only()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(_directory, "ready.txt");

        // Act
        ReadyFile.Publish(path, "Host=127.0.0.1;Password=owner-secret");
        var mode = File.GetUnixFileMode(path);

        // Assert
        mode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite, "the line carries the password of the server");
    }

    /// <summary>Deletes the test's temporary directory, ignoring IO errors so cleanup does not hide assertion failures.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Test cleanup is best-effort so teardown does not hide assertion failures.
        }
    }
}
