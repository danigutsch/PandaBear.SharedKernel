using System.Text;
using PandaBear.TestServices.Host;

namespace PandaBear.Npgsql.Tests;

/// <summary>Tests resolving and publishing the ready file through which the owner host hands over the values of its services. Each test uses its own temporary directory, removed on disposal.</summary>
public sealed class ReadyFileTests : IDisposable
{
    private readonly TemporaryDirectory temporaryDirectory = new();

    private string DirectoryPath => temporaryDirectory.Path;

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
        failure.Message.ShouldContain(ReadyFile.Variable, "the message must tell the reader which variable to set");
    }

    [Fact]
    public void A_relative_path_is_refused_because_the_owner_cannot_know_what_it_is_relative_to()
    {
        // Arrange
        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(DirectoryPath, "ready.txt"));
        Func<object?> resolve = () => ReadyFile.Resolve(FakeEnvironment.WithReadyFile(relativePath));

        // Act
        var failure = resolve.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(ReadyFile.Variable, "the message must tell the reader which variable to fix");
    }

    [Fact]
    public void A_missing_directory_is_refused_before_a_container_is_started_for_nothing()
    {
        // Arrange
        Func<object?> resolve = () => ReadyFile.Resolve(FakeEnvironment.WithReadyFile(Path.Combine(DirectoryPath, "missing", "ready.txt")));

        // Act
        var failure = resolve.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(ReadyFile.Variable, "the message must tell the reader which variable to fix");
    }

    [Fact]
    public void An_existing_file_is_refused_because_the_owner_would_read_it_as_ready_before_the_server_is()
    {
        // Arrange
        var path = Path.Combine(DirectoryPath, "ready.txt");
        File.WriteAllText(path, "Host=stale;Password=stale-secret");
        Func<object?> resolve = () => ReadyFile.Resolve(FakeEnvironment.WithReadyFile(path));

        // Act
        var failure = resolve.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(ReadyFile.Variable, "the message must tell the reader which variable to fix");
        File.ReadAllText(path).ShouldBe("Host=stale;Password=stale-secret", "a refused file is left alone");
    }

    [Fact]
    public void A_new_file_in_an_existing_directory_resolves_to_its_path_without_surrounding_whitespace()
    {
        // Arrange
        var path = Path.Combine(DirectoryPath, "ready.txt");

        // Act
        var resolved = ReadyFile.Resolve(FakeEnvironment.WithReadyFile($"  {path}\n"));

        // Assert
        resolved.ShouldBe(path);
        File.Exists(path).ShouldBeFalse("resolving the location must not create the file, which would announce a server that is not there");
    }

    [Fact]
    public void Each_value_is_published_as_one_utf8_name_value_line_without_a_byte_order_mark()
    {
        // Arrange
        var path = Path.Combine(DirectoryPath, "ready.txt");
        const string connectionString = "Host=127.0.0.1;Port=41234;Username=postgres;Password=\"p\u00e4ssw\u00f6rd\"";

        // Act
        ReadyFile.Publish(path, [("APP_DATABASE", connectionString), ("APP_ANALYTICS", "Endpoint=http://localhost:3000;WebsiteId=a1")]);
        var content = File.ReadAllBytes(path);

        // Assert
        content.ShouldBe(
            Encoding.UTF8.GetBytes("APP_DATABASE=" + connectionString + "\nAPP_ANALYTICS=Endpoint=http://localhost:3000;WebsiteId=a1\n"),
            "the owner reads one NAME=value line per variable; a byte order mark would become part of the first name");
    }

    [Theory]
    [InlineData("APP_DATABASE", "Host=127.0.0.1;Password=owner\nsecret")]
    [InlineData("APP_DATABASE", "Host=127.0.0.1;Password=owner\tsecret")]
    [InlineData("APP_DATABASE", "")]
    [InlineData("APP=DATABASE", "Host=127.0.0.1;Password=owner-secret")]
    [InlineData("", "Host=127.0.0.1;Password=owner-secret")]
    public void A_value_or_name_that_cannot_travel_as_one_line_is_refused_without_repeating_the_value_and_nothing_is_written(string variable, string value)
    {
        // Arrange
        var path = Path.Combine(DirectoryPath, "ready.txt");
        Action publish = () => ReadyFile.Publish(path, [(variable, value)]);

        // Act
        var failure = publish.ShouldThrow<ArgumentException>();

        // Assert
        failure.Message.ShouldNotContain("secret", StringComparison.Ordinal);
        Directory.GetFiles(DirectoryPath).ShouldBeEmpty("the owner would read a partial file as the values of the services");
    }

    [Fact]
    public void Publishing_replaces_the_file_in_one_step_and_leaves_no_temporary_file()
    {
        // Arrange
        var path = Path.Combine(DirectoryPath, "ready.txt");
        ReadyFile.Publish(path, [("APP_DATABASE", "Host=first")]);

        // Act
        ReadyFile.Publish(path, [("APP_DATABASE", "Host=second")]);
        var files = Directory.GetFiles(DirectoryPath).Select(Path.GetFileName).ToArray();

        // Assert
        File.ReadAllText(path).ShouldBe("APP_DATABASE=Host=second\n");
        files.ShouldBe(["ready.txt"], "the lines are written to a temporary file first and moved over the ready file, so the owner never reads a partial file");
    }

    [Fact]
    public void Nothing_appears_at_the_ready_path_until_the_whole_line_is_written()
    {
        // Arrange
        var path = Path.Combine(DirectoryPath, "ready.txt");

        // The temporary file cannot be created, which stands for a write that fails half way.
        _ = Directory.CreateDirectory(path + ".tmp");
        Action publish = () => ReadyFile.Publish(path, [("APP_DATABASE", "Host=127.0.0.1;Password=owner-secret")]);

        // Act
        _ = publish.ShouldThrow<UnauthorizedAccessException>();

        // Assert
        File.Exists(path).ShouldBeFalse("the owner treats the existence of the ready file as the signal that every line is complete");
    }

    [Fact]
    public void The_published_file_is_readable_by_its_owner_only()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(DirectoryPath, "ready.txt");

        // Act
        ReadyFile.Publish(path, [("APP_DATABASE", "Host=127.0.0.1;Password=owner-secret")]);
        var mode = File.GetUnixFileMode(path);

        // Assert
        mode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite, "the line carries the password of the server");
    }

    /// <summary>Deletes the test's temporary directory, ignoring IO errors so cleanup does not hide assertion failures.</summary>
    public void Dispose()
    {
        try
        {
            temporaryDirectory.Dispose();
        }
        catch (IOException)
        {
            // Test cleanup is best-effort so teardown does not hide assertion failures.
        }
    }
}
