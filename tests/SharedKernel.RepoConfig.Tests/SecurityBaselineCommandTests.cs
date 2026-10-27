using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>security baseline</c> command's document policy checks and the <c>security</c> option parser.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class SecurityBaselineCommandTests
{
    /// <summary>A policy that requires the text "Controls" in <c>docs/security.md</c>, which contains it, makes <c>security baseline</c> exit with 0 and print that the baseline is current.</summary>
    [Fact]
    public async Task Baseline_accepts_repository_document_contracts()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/security-baseline.json", """{"version":1,"documents":[{"path":"docs/security.md","requiredText":["Controls"]}]}""");
        workspace.WriteFile("docs/security.md", "# Controls\nDocumented policy.");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var result = await RepoConfigToolRunner.Run(["security", "baseline", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(0);
        output.ToString().ShouldContain("Security baseline is current.", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

    /// <summary><c>security baseline</c> exits with 2 and reports a <c>security:</c> error for a policy that is empty, has version 2, lists no documents, points at <c>../outside.md</c>, or points at a non-document <c>.cs</c> file.</summary>
    /// <param name="policy">The security baseline policy JSON.</param>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":2,\"documents\":[]}")]
    [InlineData("{\"version\":1,\"documents\":[]}")]
    [InlineData("{\"version\":1,\"documents\":[{\"path\":\"../outside.md\",\"requiredText\":[]}]}")]
    [InlineData("{\"version\":1,\"documents\":[{\"path\":\"docs/security.cs\",\"requiredText\":[]}]}")]
    public async Task Baseline_rejects_invalid_or_escaping_document_policy(string policy)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/security-baseline.json", policy);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var result = await RepoConfigToolRunner.Run(["security", "baseline", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(2);
        error.ToString().ShouldContain("security:", StringComparison.Ordinal);
    }

    /// <summary>When the policy names <c>docs/security.md</c> but the file does not exist, <c>security baseline</c> exits with 1 and the error names that path.</summary>
    [Fact]
    public async Task Baseline_reports_missing_documents_without_printing_document_contents()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/security-baseline.json", """{"version":1,"documents":[{"path":"docs/security.md","requiredText":[]}]}""");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var result = await RepoConfigToolRunner.Run(["security", "baseline", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("docs/security.md", StringComparison.Ordinal);
    }

    /// <summary>When <c>docs/security.md</c> lacks the required text "#815", <c>security baseline</c> exits with 1 and reports missing required text without echoing the document's content.</summary>
    [Fact]
    public async Task Baseline_reports_missing_required_text()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/security-baseline.json", """{"version":1,"documents":[{"path":"docs/security.md","requiredText":["#815"]}]}""");
        workspace.WriteFile("docs/security.md", "# Policy\nprivate-document-value");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var result = await RepoConfigToolRunner.Run(["security", "baseline", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("required text", StringComparison.Ordinal);
        error.ToString().ShouldNotContain("private-document-value", StringComparison.Ordinal);
    }

    /// <summary>When <c>docs</c> is a symbolic link to a directory outside the repository, <c>security baseline</c> exits with 2 and does not print the outside document's content.</summary>
    [Fact]
    public async Task Baseline_rejects_symbolic_links_outside_the_repository()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/security-baseline.json", """{"version":1,"documents":[{"path":"docs/security.md","requiredText":[]}]}""");
        outside.WriteFile("security.md", "outside-private-value");
        Directory.CreateSymbolicLink(Path.Combine(workspace.RootPath, "docs"), outside.RootPath);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var result = await RepoConfigToolRunner.Run(["security", "baseline", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(2);
        error.ToString().ShouldNotContain("outside-private-value", StringComparison.Ordinal);
    }
    /// <summary>The <c>security</c> command exits with 1, prints an error, does not report the baseline as current, and writes no report for <c>scan</c> without <c>--output</c>, a missing, blank, or repeated <c>--output</c> value, <c>--output</c> on <c>baseline</c>, or an unknown option.</summary>
    /// <param name="arguments">The arguments passed after <c>security</c>.</param>
    [Theory]
    [InlineData("scan")]
    [InlineData("scan", "--output")]
    [InlineData("scan", "--output", " ")]
    [InlineData("scan", "--output", "one", "--output", "two")]
    [InlineData("baseline", "--output", "reports")]
    [InlineData("baseline", "--unknown")]
    public async Task Parser_rejects_invalid_security_command_options(params string[] arguments)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/security-baseline.json", """{"version":1,"documents":[{"path":"docs/security.md","requiredText":[]}]}""");
        workspace.WriteFile("docs/security.md", "Policy");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var result = await RepoConfigToolRunner.Run(["security", .. arguments], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldNotBe(string.Empty);
        output.ToString().ShouldNotContain("Security baseline is current.", StringComparison.Ordinal);
        workspace.FileExists("reports/gitleaks.sarif").ShouldBeFalse();
    }
}
