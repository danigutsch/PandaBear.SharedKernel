namespace SharedKernel.Repositories.Tests;

/// <summary>Verifies provenance and output of optional restore comparison receipts.</summary>
public sealed class NuGetRestoreVerificationTests
{
    private const string Receipt = """
        {
          "revision": "a83647a018c100bb51f2a39a04ac7821740a8e5f",
          "status": "failed",
          "slices": ["hosted-tests", "fast-validation"],
          "phases": [
            { "name": "restore", "status": "passed" },
            { "name": "build", "status": "passed" },
            { "name": "test", "status": "failed" }
          ]
        }
        """;

    [Fact]
    public async Task Read_preserves_applicable_results_and_verification_scope()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("manifest.json", Receipt);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var verification = await NuGetRestoreVerificationReader.Read(workspace.RootPath, "manifest.json",
            "a83647a018c100bb51f2a39a04ac7821740a8e5f", dirty: false, cancellationToken);

        // Assert
        verification.ShouldNotBeNull();
        verification.Status.ShouldBe("failed");
        verification.Restore.ShouldBe("passed");
        verification.Build.ShouldBe("passed");
        verification.Test.ShouldBe("failed");
        verification.Slices.ShouldBe(["fast-validation", "hosted-tests"]);
    }

    [Theory]
    [InlineData("a83647a018c100bb51f2a39a04ac7821740a8e5f", true)]
    [InlineData("6c2573c3889f65133368a165afa445ddd5c79229", false)]
    public async Task Read_fails_if_the_receipt_does_not_apply_to_the_checkout(string revision, bool dirty)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("manifest.json", Receipt);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        Func<Task> read = () => NuGetRestoreVerificationReader.Read(
            workspace.RootPath, "manifest.json", revision, dirty, cancellationToken);

        // Assert
        await read.ShouldThrow<InvalidDataException>();
    }
}
