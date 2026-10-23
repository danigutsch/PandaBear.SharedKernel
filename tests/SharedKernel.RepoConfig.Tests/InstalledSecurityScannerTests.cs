using System.Globalization;
using System.Security.Cryptography;

namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class InstalledSecurityScannerTests
{
    [Fact(Explicit = true)]
    public async Task Installed_scanner_detects_secrets_redacts_evidence_and_accepts_clean_source()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("repository with spaces/.config/security-baseline.json", """{"version":1,"documents":[{"path":"policy.md","requiredText":["Controls"]}]}""");
        workspace.WriteFile("repository with spaces/policy.md", "# Controls\n");
        var secret = "ghp_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20));
        workspace.WriteFile("repository with spaces/fixture.txt", "token = \"" + secret + "\"\n");
        var root = Path.Combine(workspace.RootPath, "repository with spaces");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var detected = await RepoConfigToolRunner.Run(["security", "scan", "--root", root, "--output", "reports"], output, error, root, TestContext.Current.CancellationToken);
        var report = workspace.ReadFile("repository with spaces/reports/gitleaks.sarif");
        workspace.WriteFile("repository with spaces/fixture.txt", "Clean source.\n");
        var clean = await RepoConfigToolRunner.Run(["security", "scan", "--root", root, "--output", "clean reports"], output, error, root, TestContext.Current.CancellationToken);
        var console = output.ToString() + error.ToString();

        // Assert
        detected.ShouldBe(1);
        clean.ShouldBe(0);
        report.ShouldNotContain(secret, StringComparison.Ordinal);
        console.ShouldNotContain(secret, StringComparison.Ordinal);
    }
}
