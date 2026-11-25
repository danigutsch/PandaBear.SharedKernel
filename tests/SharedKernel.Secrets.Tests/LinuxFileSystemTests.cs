namespace SharedKernel.Secrets.Tests;

/// <summary>Tests the glibc calls that make a replacement atomic and durable.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class LinuxFileSystemTests
{
    [Fact(SkipType = typeof(MemoryFileSystem), SkipUnless = nameof(MemoryFileSystem.Linux), Skip = "The atomic exchange is Linux only.")]
    public void Two_names_swap_their_files_in_one_step()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var first = Path.Combine(directory.Path, "first");
        var second = Path.Combine(directory.Path, "second");
        File.WriteAllText(first, "1");
        File.WriteAllText(second, "2");

        // Act
        var exchanged = LinuxFileSystem.TryExchange(first, second);

        // Assert
        exchanged.ShouldBeTrue();
        File.ReadAllText(first).ShouldBe("2");
        File.ReadAllText(second).ShouldBe("1");
    }

    [Fact(SkipType = typeof(MemoryFileSystem), SkipUnless = nameof(MemoryFileSystem.Linux), Skip = "The atomic exchange is Linux only.")]
    public void Exchanging_with_a_missing_file_is_an_error_not_a_fallback()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var present = Path.Combine(directory.Path, "present");
        File.WriteAllText(present, "1");

        // Act
        Action exchange = () => LinuxFileSystem.TryExchange(present, Path.Combine(directory.Path, "missing"));

        // Assert
        exchange.ShouldThrow<IOException>();
        File.ReadAllText(present).ShouldBe("1");
    }

    [Fact]
    public void A_directory_is_flushed_to_disk_or_skipped_where_that_is_not_possible()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        LinuxFileSystem.FlushDirectory(directory.Path);

        // Assert
        Directory.Exists(directory.Path).ShouldBeTrue();
    }

    [Fact(SkipType = typeof(MemoryFileSystem), SkipUnless = nameof(MemoryFileSystem.Linux), Skip = "The atomic exchange is Linux only.")]
    public void A_missing_directory_cannot_be_flushed()
    {
        // Act
        var flush = () => LinuxFileSystem.FlushDirectory("/no/such/directory");

        // Assert
        flush.ShouldThrow<IOException>();
    }
}
