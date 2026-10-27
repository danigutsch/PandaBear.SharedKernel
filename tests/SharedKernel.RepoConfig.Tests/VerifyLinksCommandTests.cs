namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Tests the verify-links command against seeded Git repositories of Markdown files.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class VerifyLinksCommandTests
{
    private const string DocsConfiguration = """{"version":1,"links":{"include":["docs/*.md"],"exclude":[]}}""";
    private const string Tracker = "summarize durable context or link a maintained doc instead";

    /// <summary>
    /// Verifies that the findings match those reported by the original Python linter for the seeded corpus.
    /// </summary>
    [Fact]
    public async Task Verify_links_reports_what_the_python_linter_reports_for_the_seeded_corpus()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = await LinkTestRepository.CreateGit(seedCorpus: true, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(["verify-links", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldBe(LinkTestRepository.Lines(LinkCorpus.ExpectedLinkFindings));
    }

    /// <summary>
    /// Verifies that explicit paths replace the configured include patterns while the exclude patterns still apply.
    /// </summary>
    [Fact]
    public async Task Verify_links_scans_explicit_paths_instead_of_the_configured_include_but_keeps_the_exclude()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = await LinkTestRepository.CreateGit(seedCorpus: true, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(["verify-links", .. LinkCorpus.ExplicitPaths, "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe(LinkTestRepository.Lines(LinkCorpus.ExpectedExplicitPathFindings));
    }

    /// <summary>
    /// Verifies that the success output counts the scanned files when every link resolves.
    /// </summary>
    [Fact]
    public async Task Verify_links_counts_the_scanned_files_when_every_link_resolves()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = await LinkTestRepository.CreateGit(seedCorpus: true, TestContext.Current.CancellationToken);
        using var workingDirectory = new TemporaryDirectory();

        // Act
        var result = await repository.RunCommand(
            ["verify-links", "docs/sub", "README.md", "src/App/bin/x.md", "docs/sub/other.png", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            workingDirectory.Path);

        // Assert
        result.ExitCode.ShouldBe(0, "Explicit paths resolve against --root; the excluded file and the non-Markdown path are skipped.");
        result.StandardError.ShouldBe(string.Empty);
        result.StandardOutput.ShouldBe("Link validation passed for 2 documentation files." + Environment.NewLine);
    }

    /// <summary>
    /// Verifies that line splitting, lowercasing, and URL parsing match the Python linter.
    /// </summary>
    [Fact]
    public async Task Verify_links_matches_python_line_splitting_lowercasing_and_url_parsing()
    {
        // Arrange
        using var workspace = LinkTestRepository.CreateWorkspace(DocsConfiguration);
        workspace.WriteFile(
            "docs/u.md",
            "# \u00dcn\u00efcode \u0130stanbul Kelvin\u212a\n[k2](#ncode-istanbul-kelvink)\n[k3](#NCODE-%C4%B0STANBUL-KELVINK)\n"
            + "[k4](#ncode-istanbul-kelvin%E2%84%AA)\nissues/945\u00b2\n#945\u0301\nPR-1_\nhttps://GitHub.com/o/r/issues/1\n"
            + "https://github.com/o/r/issues/1;x\nhttps://github.com/o/r/issues/\u00b2\n(https://github.com/o/r/pull/3?x#y)\n"
            + "http://github.com/o/r/issues\n\u0001https://github.com/o/r/issues/4\n[x](%ZZ.md)\n[x](%C3%28.md)\n"
            + "a\u2028[x](m1.md)\n\u0085[x](m2.md)\n\u001f[r]: m3.md\n\u001d# Grp\n[g](#grp)\n");

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-links", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe(LinkTestRepository.Lines(
            "Link validation failed:",
            $"- docs/u.md:6: bare issue/PR reference is not allowed; {Tracker}",
            $"- docs/u.md:9: direct GitHub issue/PR link is not allowed; {Tracker}",
            $"- docs/u.md:10: direct GitHub issue/PR link is not allowed; {Tracker}",
            $"- docs/u.md:11: direct GitHub issue/PR link is not allowed; {Tracker}",
            $"- docs/u.md:13: direct GitHub issue/PR link is not allowed; {Tracker}",
            "- docs/u.md:3: missing Markdown anchor 'NCODE-%C4%B0STANBUL-KELVINK' in docs/u.md",
            "- docs/u.md:14: missing local link target: %ZZ.md",
            "- docs/u.md:15: missing local link target: %C3%28.md",
            "- docs/u.md:17: missing local link target: m1.md",
            "- docs/u.md:19: missing local link target: m2.md",
            "- docs/u.md:20: missing local link target: m3.md"));
    }

    /// <summary>
    /// Verifies that a line containing a tracker reference is rejected.
    /// </summary>
    [Theory]
    [InlineData("See https://github.com/example/project/issues/945.")]
    [InlineData("See https://github.com/example/project/pull/945.")]
    [InlineData("See `https://github.com/example/project/issues/945`.")]
    [InlineData("See `issues/945`.")]
    [InlineData("Tracked separately in #945.")]
    [InlineData("- #945")]
    [InlineData("Tracked as GH-945.")]
    [InlineData("Tracked as example/project#945.")]
    [InlineData("Output: `TestResults/issue-945-benchmark.tsv`.")]
    [InlineData("Tracked separately in #123456.")]
    public async Task Verify_links_rejects_tracker_references(string line)
    {
        // Arrange
        using var workspace = LinkTestRepository.CreateWorkspace(DocsConfiguration);
        workspace.WriteFile("docs/example.md", line + "\n");

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-links", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("- docs/example.md:1: ", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a hash that is not a tracker reference is allowed.
    /// </summary>
    [Theory]
    [InlineData("Unicode Standard Annex #15: Unicode Normalization Forms")]
    [InlineData("UAX #15 defines normalization forms.")]
    [InlineData("Use a PKCS#12 certificate.")]
    [InlineData("style node fill:#e7f5ff,stroke:#123456")]
    [InlineData("See https://example.com/reference/#123.")]
    [InlineData("Revision 945 remains supported.")]
    public async Task Verify_links_allows_non_tracker_hashes(string line)
    {
        // Arrange
        using var workspace = LinkTestRepository.CreateWorkspace(DocsConfiguration);
        workspace.WriteFile("docs/example.md", line + "\n");

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-links", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe("Link validation passed for 1 documentation files." + Environment.NewLine);
    }

    /// <summary>
    /// Verifies that a file or link the Python linter cannot process is reported with the expected finding.
    /// </summary>
    [Theory]
    [InlineData("docs/missing.md", "- docs/missing.md: cannot be read.")]
    [InlineData("docs/invalid.md", "- docs/invalid.md: is not valid UTF-8.")]
    [InlineData("docs/empty.md", "- docs/empty.md:1: empty link target.")]
    public async Task Verify_links_reports_a_file_or_link_that_the_python_linter_cannot_process(string path, string expectedFinding)
    {
        // Arrange
        using var workspace = LinkTestRepository.CreateWorkspace(DocsConfiguration);
        File.WriteAllBytes(Path.Combine(workspace.RootPath, "docs", "invalid.md"), [0x23, 0x20, 0xFF, 0x0A]);
        workspace.WriteFile("docs/empty.md", "[x]( )\n");

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-links", path, "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe(LinkTestRepository.Lines("Link validation failed:", expectedFinding));
    }

    /// <summary>
    /// Verifies that a configuration without a links section is rejected.
    /// </summary>
    [Theory]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"version":1,"links":null}""")]
    public async Task Verify_links_requires_the_links_section(string configuration)
    {
        // Arrange
        using var workspace = LinkTestRepository.CreateWorkspace(configuration);

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-links", "docs", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: .config/sharedkernel-repo.json has no links section." + Environment.NewLine);
    }

    /// <summary>
    /// Verifies that an invalid links section is rejected with an error naming the file and JSON path.
    /// </summary>
    [Theory]
    [InlineData("""{"version":1,"links":{"include":["*.md"]}}""", ".config/sharedkernel-repo.json is not valid at $.links (line 1).")]
    [InlineData("""{"version":1,"links":{"include":["*.md"],"exclude":[],"extra":1}}""", ".config/sharedkernel-repo.json is not valid at $.links.extra (line 1).")]
    [InlineData("""{"version":1,"links":{"include":[],"exclude":[]}}""", ".config/sharedkernel-repo.json: $.links.include must name at least one pattern.")]
    [InlineData("""{"version":1,"links":{"include":["../*.md"],"exclude":[]}}""", ".config/sharedkernel-repo.json: $.links.include[0] must be a repository-relative path pattern with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("""{"version":1,"links":{"include":["*.md"],"exclude":["docs\\*.md"]}}""", ".config/sharedkernel-repo.json: $.links.exclude[0] must be a repository-relative path pattern with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("""{"version":1,"links":{"include":["*.md","/docs/*.md"],"exclude":[]}}""", ".config/sharedkernel-repo.json: $.links.include[1] must be a repository-relative path pattern with '/' separators and without empty, '.', or '..' segments.")]
    public async Task Verify_links_names_the_file_and_json_path_of_an_invalid_links_section(string configuration, string expectedError)
    {
        // Arrange
        using var workspace = LinkTestRepository.CreateWorkspace(configuration);

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-links", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldStartWith("sharedkernel-repo: " + expectedError, StringComparison.Ordinal);
    }
}
