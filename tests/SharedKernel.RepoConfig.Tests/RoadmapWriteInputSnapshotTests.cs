using System.Globalization;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapWriteInputSnapshotTests
{
    [Fact]
    public async Task Verify_reports_a_deleted_required_file_as_a_stale_plan()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        var snapshot = RoadmapWriteInputSnapshot.Capture(workspace.RootPath);
        var deletedPath = Path.Combine(workspace.RootPath, "roadmap", "config.json");
        workspace.DeleteFile("roadmap/config.json");
        Action verify = snapshot.Verify;

        // Act
        var exception = verify.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("changed after the write plan was created", StringComparison.Ordinal);
        exception.Message.ShouldContain(deletedPath, StringComparison.Ordinal);
    }
}
