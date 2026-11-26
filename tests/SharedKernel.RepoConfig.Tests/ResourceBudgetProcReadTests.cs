namespace SharedKernel.RepoConfig.Tests;

/// <summary>Checks Linux procfs disappearance and read failures at the cleanup observation boundary.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class ResourceBudgetProcReadTests
{
    private const int NoSuchProcess = 3;

    /// <summary>Gets whether procfs fixtures can run.</summary>
    public static bool Linux => OperatingSystem.IsLinux();

    [Fact(SkipUnless = nameof(Linux), Skip = "The procfs fixture requires Linux.")]
    public async Task Reaped_process_descriptor_counts_as_a_complete_observation()
    {
        // Arrange
        await using var stat = await ResourceBudgetProcessFixture.OpenReapedStat();
        var descriptor = stat.SafeFileHandle.DangerousGetHandle().ToInt64();
        var descriptorPath = FormattableString.Invariant($"/proc/self/fd/{descriptor}");
        var failure = ShouldAssertionExtensions.ShouldThrow<IOException>(() => File.ReadAllText(descriptorPath));
        failure.HResult.ShouldBe(NoSuchProcess);

        // Act
        var contents = ResourceBudgetProcess.ReadProc(descriptorPath, out var readable);

        // Assert
        contents.ShouldBeNull();
        readable.ShouldBeTrue("The kernel confirmed that the retained procfs entry no longer has a process.");
    }

    [Fact(SkipUnless = nameof(Linux), Skip = "The procfs fixture requires Linux.")]
    public void Missing_entry_counts_as_a_complete_observation()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var missing = Path.Combine(directory.Path, "missing");

        // Act
        var contents = ResourceBudgetProcess.ReadProc(missing, out var readable);

        // Assert
        contents.ShouldBeNull();
        readable.ShouldBeTrue();
    }

    [Fact(SkipUnless = nameof(Linux), Skip = "The procfs fixture requires Linux.")]
    public void Directory_read_failure_counts_as_an_incomplete_observation()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var contents = ResourceBudgetProcess.ReadProc(directory.Path, out var readable);

        // Assert
        contents.ShouldBeNull();
        readable.ShouldBeFalse("The path exists and cannot be read as a file.");
    }

    [Fact(SkipUnless = nameof(Linux), Skip = "The procfs fixture requires Linux.")]
    public void Symbolic_link_loop_counts_as_an_incomplete_observation()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var link = Path.Combine(directory.Path, "loop");
        File.CreateSymbolicLink(link, link);

        // Act
        var contents = ResourceBudgetProcess.ReadProc(link, out var readable);

        // Assert
        contents.ShouldBeNull();
        readable.ShouldBeFalse("A symbolic link loop is a read error.");
    }
}
