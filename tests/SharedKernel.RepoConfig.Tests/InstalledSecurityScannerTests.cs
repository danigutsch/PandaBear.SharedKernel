using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the security scanner that is installed on the machine. The tests run only when requested.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class InstalledSecurityScannerTests
{
    /// <summary>Verifies that the installed scanner finds planted secrets, redacts the evidence it reports, and accepts a clean source tree, including in a directory name with spaces.</summary>
    [Theory(Explicit = true)]
    [InlineData("repository with spaces")]
    [InlineData(".worktrees/repository with spaces")]
    public async Task Installed_scanner_detects_secrets_redacts_evidence_and_accepts_clean_source(string repositoryDirectory)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile($"{repositoryDirectory}/.config/security-baseline.json", """{"version":1,"documents":[{"path":"policy.md","requiredText":["Controls"]}]}""");
        workspace.WriteFile($"{repositoryDirectory}/policy.md", "# Controls\n");
        workspace.WriteFile($"{repositoryDirectory}/.gitleaks.toml", """
            [extend]
            useDefault = true
            [[allowlists]]
            paths = ['''(^|/)\.worktrees/''']
            """);
        var secret = "ghp_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20));
        workspace.WriteFile($"{repositoryDirectory}/.worktrees/ignored.fixture", "token = \"" + secret + "\"\n");
        workspace.WriteFile($"{repositoryDirectory}/fixture.txt", "token = \"" + secret + "\"\n");
        var root = Path.Combine(workspace.RootPath, repositoryDirectory);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var detected = await RepoConfigToolRunner.Run(["security", "scan", "--root", root, "--output", "reports"], output, error, root, TestContext.Current.CancellationToken);
        var report = workspace.ReadFile($"{repositoryDirectory}/reports/gitleaks.sarif");
        workspace.WriteFile($"{repositoryDirectory}/fixture.txt", "Clean source.\n");
        var clean = await RepoConfigToolRunner.Run(["security", "scan", "--root", root, "--output", "clean reports"], output, error, root, TestContext.Current.CancellationToken);
        var console = output.ToString() + error.ToString();

        // Assert
        detected.ShouldBe(1);
        clean.ShouldBe(0);
        report.ShouldNotContain(secret, StringComparison.Ordinal);
        console.ShouldNotContain(secret, StringComparison.Ordinal);
        report.ShouldNotContain("ignored.fixture", StringComparison.Ordinal);
        using var sarif = JsonDocument.Parse(report);
        var reportUri = sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation")
            .GetProperty("uri").GetString().ShouldNotBeNull();
        Uri.TryCreate(reportUri, UriKind.Absolute, out _).ShouldBeFalse();
    }
}
