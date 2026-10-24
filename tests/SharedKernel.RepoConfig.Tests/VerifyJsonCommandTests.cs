using System.Text;

namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class VerifyJsonCommandTests
{
    private const string TrackedPath = " settings café.json";
    private const string Jsonc = "// Repository JSONC\n{\"url\":\"https://example.test/a//b\",\"values\":[1,],/* comment */}";

    [Theory]
    [InlineData("jsonc")]
    [InlineData("utf8-bom")]
    public async Task Verify_json_accepts_tracked_jsonc_without_traversing_untracked_caches(string scenario)
    {
        // Arrange
        using var repository = await VerifyJsonTestRepository.Create(TestContext.Current.CancellationToken);
        byte[] content = scenario == "utf8-bom"
            ? [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Jsonc)]
            : Encoding.UTF8.GetBytes(Jsonc);
        repository.WriteBytes(TrackedPath, content);
        await repository.Stage(TrackedPath, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(["verify-json", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldContain("Repository JSON is valid.", StringComparison.Ordinal);
        result.StandardError.ShouldBe(string.Empty, "Untracked JSON under .nuget is not read.");
    }

    [Theory]
    [InlineData("malformed", "Invalid JSON at line 1, byte 10: Expected depth to be zero at the end of the JSON payload.")]
    [InlineData("invalid-utf8", "Invalid UTF-8 at line 1, byte 3.")]
    [InlineData("empty", "Invalid JSON at line 1, byte 1: The input does not contain any JSON tokens.")]
    [InlineData("missing-tracked-file", "Is missing from the working tree.")]
    public async Task Verify_json_reports_the_tracked_path_and_location_of_each_failure(string scenario, string expectedMessage)
    {
        // Arrange
        using var repository = await VerifyJsonTestRepository.Create(TestContext.Current.CancellationToken);
        byte[] content = scenario switch
        {
            "malformed" => "{\"value\":"u8.ToArray(),
            "invalid-utf8" => [0x7B, 0x22, 0xFF, 0x22, 0x3A, 0x31, 0x7D],
            "empty" => [],
            _ => Encoding.UTF8.GetBytes(Jsonc),
        };
        repository.WriteBytes(TrackedPath, content);
        await repository.Stage(TrackedPath, TestContext.Current.CancellationToken);
        if (scenario == "missing-tracked-file")
        {
            repository.Delete(TrackedPath);
        }

        // Act
        var result = await repository.RunCommand(["verify-json", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldContain("Repository JSON verification failed:", StringComparison.Ordinal);
        result.StandardError.ShouldContain($"- {TrackedPath}: {expectedMessage}", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(".nuget", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(repository.RootPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_json_reports_every_malformed_file()
    {
        // Arrange
        using var repository = await VerifyJsonTestRepository.Create(TestContext.Current.CancellationToken);
        repository.WriteText(TrackedPath, "{\"value\":");
        repository.WriteText("nested/another.json", "{");
        repository.WriteText("valid.json", "{}");
        await repository.Stage(TrackedPath, TestContext.Current.CancellationToken);
        await repository.Stage("nested/another.json", TestContext.Current.CancellationToken);
        await repository.Stage("valid.json", TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(["verify-json", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain($"- {TrackedPath}: Invalid JSON", StringComparison.Ordinal);
        result.StandardError.ShouldContain("- nested/another.json: Invalid JSON", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("valid.json", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}", "{\"value\":", 1)]
    [InlineData("{\"value\":", "{}", 0)]
    public async Task Verify_json_reads_working_tree_content_instead_of_the_staged_blob(string staged, string workingTree, int expectedExitCode)
    {
        // Arrange
        using var repository = await VerifyJsonTestRepository.Create(TestContext.Current.CancellationToken);
        repository.WriteText("settings.json", staged);
        await repository.Stage("settings.json", TestContext.Current.CancellationToken);
        repository.WriteText("settings.json", workingTree);

        // Act
        var result = await repository.RunCommand(["verify-json", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(expectedExitCode, "The modified file is not staged, so only the working tree holds its current content.");
    }

    [Fact]
    public async Task Verify_json_rejects_a_file_larger_than_the_size_limit()
    {
        // Arrange
        using var repository = await VerifyJsonTestRepository.Create(TestContext.Current.CancellationToken);
        repository.WriteText("large.json", "{}");
        await repository.Stage("large.json", TestContext.Current.CancellationToken);
        await using (var stream = File.Open(Path.Combine(repository.RootPath, "large.json"), FileMode.Create))
        {
            stream.SetLength((64 * 1024 * 1024) + 1);
        }

        // Act
        var result = await repository.RunCommand(["verify-json", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("- large.json: Exceeds the 64 MiB JSON size limit.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_json_does_not_follow_a_tracked_symbolic_link()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = await VerifyJsonTestRepository.Create(TestContext.Current.CancellationToken);
        using var outside = new TemporaryDirectory();
        var outsidePath = Path.Combine(outside.Path, "outside.json");
        await File.WriteAllTextAsync(outsidePath, "{", TestContext.Current.CancellationToken);
        File.CreateSymbolicLink(Path.Combine(repository.RootPath, "linked.json"), outsidePath);
        await repository.Stage("linked.json", TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(["verify-json", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, "Git tracks the link itself; the malformed file it points to lies outside the repository.");
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Verify_json_uses_the_working_directory_when_root_is_omitted()
    {
        // Arrange
        using var repository = await VerifyJsonTestRepository.Create(TestContext.Current.CancellationToken);
        repository.WriteText("settings.json", "{");
        await repository.Stage("settings.json", TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(["verify-json"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("- settings.json: Invalid JSON", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_json_reports_an_unreadable_index_without_raw_git_details()
    {
        // Arrange
        using var repository = await VerifyJsonTestRepository.Create(TestContext.Current.CancellationToken);
        repository.WriteText("settings.json", "{}");
        await repository.Stage("settings.json", TestContext.Current.CancellationToken);
        repository.CorruptIndex();

        // Act
        var result = await repository.RunCommand(["verify-json", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: Git index inspection failed." + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_json_rejects_arguments_instead_of_skipping_validation()
    {
        // Arrange
        using var repository = await VerifyJsonTestRepository.Create(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(["verify-json", "roadmap", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Unrecognized command or argument 'roadmap'.", StringComparison.Ordinal);
        result.StandardOutput.ShouldNotContain("Repository JSON is valid.", StringComparison.Ordinal);
    }
}
