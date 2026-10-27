namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Tests the verify-line-endings command against seeded Git repositories with eol attributes.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class VerifyLineEndingsCommandTests
{
    private const string Attributes = "* text=auto eol=lf\n*.cmd text eol=crlf\n*.png binary\n";

    [Fact]
    public async Task Verify_line_endings_rejects_a_crlf_blob_in_the_index_under_eol_lf()
    {
        // Arrange
        using var repository = await LinkTestRepository.CreateGit(seedCorpus: false, TestContext.Current.CancellationToken);
        repository.WriteText("blob.txt", "blob\r\n");
        var objectId = await repository.StoreBlob("blob.txt", TestContext.Current.CancellationToken);
        repository.WriteText(".gitattributes", Attributes);
        await repository.Stage(".gitattributes", TestContext.Current.CancellationToken);
        await repository.SetIndexEntry("100644", objectId, "blob.txt", TestContext.Current.CancellationToken);
        repository.WriteText("blob.txt", "blob\n");

        // Act
        var result = await repository.RunCommand(["verify-line-endings", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1, "The working tree holds LF, so only the staged blob has CRLF.");
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldBe(
            "Repository line-ending verification failed:" + Environment.NewLine
            + "- blob.txt: Expected LF line endings." + Environment.NewLine);
    }

    [Theory]
    [InlineData("worktree.md", "# Worktree\r\n", "Expected LF line endings.")]
    [InlineData("mixed.txt", "a\r\nb\n", "Expected LF line endings.")]
    [InlineData("script.cmd", "echo lf\n", "Expected CRLF line endings.")]
    public async Task Verify_line_endings_rejects_working_tree_content_that_the_attributes_do_not_allow(string path, string workingTree, string expectedMessage)
    {
        // Arrange
        using var repository = await LinkTestRepository.CreateGit(seedCorpus: false, TestContext.Current.CancellationToken);
        repository.WriteText(".gitattributes", Attributes);
        repository.WriteText(path, "staged\n");
        await repository.Stage(".gitattributes", TestContext.Current.CancellationToken);
        await repository.Stage(path, TestContext.Current.CancellationToken);
        repository.WriteText(path, workingTree);

        // Act
        var result = await repository.RunCommand(["verify-line-endings", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain($"- {path}: {expectedMessage}", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_line_endings_accepts_files_that_match_their_attributes()
    {
        // Arrange
        using var repository = await LinkTestRepository.CreateGit(seedCorpus: false, TestContext.Current.CancellationToken);
        repository.WriteText(".gitattributes", Attributes);
        repository.WriteText("ok.md", "# Ok\n");
        repository.WriteText("script.cmd", "echo crlf\r\n");
        repository.WriteText("image.png", "binary\r\n");
        repository.WriteText("gone.md", "# Gone\n");
        foreach (var path in (string[])[".gitattributes", "ok.md", "script.cmd", "image.png", "gone.md"])
        {
            await repository.Stage(path, TestContext.Current.CancellationToken);
        }

        repository.Delete("gone.md");
        repository.WriteText("untracked.md", "# Untracked\r\n");

        // Act
        var result = await repository.RunCommand(["verify-line-endings", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, "Binary files, deleted index entries, and untracked files are not checked.");
        result.StandardError.ShouldBe(string.Empty);
        result.StandardOutput.ShouldBe("Repository line endings match their attributes." + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_line_endings_reports_every_file_of_the_seeded_corpus()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = await LinkTestRepository.CreateGit(seedCorpus: true, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(["verify-line-endings", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe(string.Join(Environment.NewLine, LinkCorpus.ExpectedLineEndingFindings) + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_line_endings_fails_outside_a_git_repository()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var repository = new TemporaryGitRepository();

        // Act
        var result = await repository.RunCommand(["verify-line-endings", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: Git index inspection failed." + Environment.NewLine);
    }
}
