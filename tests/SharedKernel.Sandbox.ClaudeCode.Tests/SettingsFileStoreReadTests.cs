namespace PandaBear.Sandbox.ClaudeCode.Tests;

/// <summary>Tests reading the settings file within its size cap.</summary>
[Trait("Capability", "Sandbox")]
public sealed class SettingsFileStoreReadTests
{
    [Fact]
    public async Task A_missing_file_reads_as_null()
    {
        // Arrange
        using var root = new TemporaryDirectory();

        // Act
        var text = await SettingsFileStore.Read(Path.Combine(root.Path, "missing.json"), "missing.json", TestContext.Current.CancellationToken);

        // Assert
        text.ShouldBeNull();
    }

    [Fact]
    public async Task A_short_file_and_a_file_of_exactly_the_cap_are_read_in_full()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        var shortPath = Path.Combine(root.Path, "short.json");
        var cappedPath = Path.Combine(root.Path, "capped.json");
        await File.WriteAllTextAsync(shortPath, "{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(cappedPath, new string('a', SettingsFileStore.MaxBytes), TestContext.Current.CancellationToken);

        // Act
        var shortText = await SettingsFileStore.Read(shortPath, "short.json", TestContext.Current.CancellationToken);
        var cappedText = await SettingsFileStore.Read(cappedPath, "capped.json", TestContext.Current.CancellationToken);

        // Assert
        shortText.ShouldBe("{}");
        cappedText.ShouldNotBeNull().Length.ShouldBe(SettingsFileStore.MaxBytes);
    }

    [Fact]
    public async Task A_file_one_byte_over_the_cap_is_refused()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "large.json");
        await File.WriteAllTextAsync(path, new string('a', SettingsFileStore.MaxBytes + 1), TestContext.Current.CancellationToken);

        // Act
        Func<Task> read = () => SettingsFileStore.Read(path, "large.json", TestContext.Current.CancellationToken);
        var exception = await read.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("large.json is larger than 1 MiB.");
    }

    [Fact]
    public async Task A_byte_order_mark_is_skipped()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "bom.json");
        await File.WriteAllBytesAsync(path, [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}'], TestContext.Current.CancellationToken);

        // Act
        var text = await SettingsFileStore.Read(path, "bom.json", TestContext.Current.CancellationToken);

        // Assert
        text.ShouldBe("{}");
    }
}
