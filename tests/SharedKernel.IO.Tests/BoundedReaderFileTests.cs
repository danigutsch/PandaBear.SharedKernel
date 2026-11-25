namespace SharedKernel.IO.Tests;

/// <summary>Tests whole-file reads that refuse files over a limit.</summary>
public sealed class BoundedReaderFileTests
{
    [Fact]
    public async Task A_file_within_the_limit_is_read()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllBytesAsync(path, [1, 2, 3], TestContext.Current.CancellationToken);

        // Act
        var content = await BoundedReader.ReadFile(path, 3, TestContext.Current.CancellationToken);

        // Assert
        content.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task A_file_over_the_limit_is_refused()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllBytesAsync(path, [1, 2, 3], TestContext.Current.CancellationToken);

        // Act
        var content = await BoundedReader.ReadFile(path, 2, TestContext.Current.CancellationToken);

        // Assert
        content.ShouldBeNull();
    }

    [Fact]
    public async Task A_missing_file_throws_file_not_found()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        Func<Task> read = () => BoundedReader.ReadFile(Path.Combine(directory.Path, "missing.json"), 10, TestContext.Current.CancellationToken);

        // Assert
        await read.ShouldThrow<FileNotFoundException>();
    }

    [Fact]
    public async Task Invalid_arguments_are_rejected()
    {
        // Act
        Func<Task> empty = () => BoundedReader.ReadFile(string.Empty, 1, TestContext.Current.CancellationToken);
        Func<Task> negative = () => BoundedReader.ReadFile("file", -1, TestContext.Current.CancellationToken);

        // Assert
        await empty.ShouldThrow<ArgumentException>();
        await negative.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
