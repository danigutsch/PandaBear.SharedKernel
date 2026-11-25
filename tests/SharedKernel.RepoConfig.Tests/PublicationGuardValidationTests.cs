namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises invalid and optional publication policy inputs through the real Git command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class PublicationGuardValidationTests
{
    private const string PathOnlyPolicy = """
        {
          "version": 1,
          "neverTrack": [":(top,glob)private/**"],
          "patterns": [],
          "allowlist": []
        }
        """;
    private const string Version = """
        "version": 1
        """;
    private const string PathRules = """[":(top,glob)private/**"]""";
    private const string PatternRules = """[{"id": "marker", "regex": "synthetic-private-marker"}]""";
    private const string Allowlist = """
        "allowlist": []
        """;
    private const string PolicyError = "a valid staged publication policy is required";
    private const string AllowlistError = "every allowlist entry requires a known rule, exact path, and reason";
    private const string LocalFileError = "the optional local patterns file requires a regular file with no symbolic links";

    [Theory]
    [InlineData(Version, """ "version": "1" """, PolicyError)]
    [InlineData(Version, """ "version": 1.5 """, PolicyError)]
    [InlineData(Version, """ "version": 2 """, PolicyError)]
    [InlineData(Version, """ "version": 1, "version": 1 """, PolicyError)]
    [InlineData(PathRules, "{}", PolicyError)]
    [InlineData(PathRules, "[false]", PolicyError)]
    [InlineData(PathRules, """[" "]""", PolicyError)]
    [InlineData(PathRules, """["synthetic\npath"]""", PolicyError)]
    [InlineData(PatternRules, "false", PolicyError)]
    [InlineData(PatternRules, """[{"id": "marker"}]""", PolicyError)]
    [InlineData(PatternRules, """[{"id": "bad:marker", "regex": "synthetic"}]""", PolicyError)]
    [InlineData(PatternRules, """[{"id": "marker","regex": "one"},{"id": "marker","regex": "two"}]""", PolicyError)]
    [InlineData(Allowlist, """ "allowlist": {} """, AllowlistError)]
    [InlineData(Allowlist, """ "allowlist": [{"rule":"unknown","path":"sample.txt","reason":"Synthetic."}] """, AllowlistError)]
    [InlineData(Allowlist, """ "allowlist": [{"rule":"marker","path":"../sample.txt","reason":"Synthetic."}] """, AllowlistError)]
    public async Task Verify_publication_rejects_invalid_staged_policy(string original, string replacement, string error)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(replacement);
        var ct = TestContext.Current.CancellationToken;
        var policy = PublicationGuardTestRepository.Policy.Replace(original, replacement.Trim(), StringComparison.Ordinal);
        using var repository = await PublicationGuardTestRepository.Create(policy, ct);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe($"Publication guard failed: {error}.\n");
        result.StandardOutput.ShouldBeEmpty();
    }

    [Fact]
    public async Task Verify_publication_rejects_a_policy_without_any_rules()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var policy = PathOnlyPolicy.Replace(PathRules, "[]", StringComparison.Ordinal);
        using var repository = await PublicationGuardTestRepository.Create(policy, ct);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe($"Publication guard failed: {PolicyError}.\n");
    }

    [Fact]
    public async Task Verify_publication_rejects_a_directory_allowlist_exception()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var policy = PublicationGuardTestRepository.Policy.Replace(Allowlist,
            """ "allowlist": [{"rule": "marker", "path": "notes", "reason": "Synthetic fixture."}] """.Trim(),
            StringComparison.Ordinal);
        using var repository = await PublicationGuardTestRepository.Create(policy, ct);
        await PublicationGuardTestRepository.Stage(repository, "notes/test.txt", PublicationGuardTestRepository.Pattern, ct);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("Publication guard failed: an allowlist path must identify an exact staged file.\n");
    }

    [Theory]
    [InlineData(" \r\n # comment-only file\r\n", "skip: optional local patterns file is empty\nPublication guard passed.\n")]
    [InlineData(" # comment\r\nsynthetic-unused-marker\r\n", "Publication guard passed.\n")]
    public async Task Verify_publication_reports_if_ignored_local_patterns_were_used(string localPatterns, string expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PathOnlyPolicy, ct);
        await PublicationGuardTestRepository.Stage(repository, "notes/test.txt", "ordinary content", ct);
        repository.WriteText(PublicationGuardTestRepository.LocalPatternsPath, localPatterns);
        var nestedDirectory = Path.Combine(repository.RootPath, "notes");

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct, workingDirectory: nestedDirectory);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(expected);
        result.StandardError.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("synthetic\tlocal", "could not read the optional local patterns file")]
    [InlineData("[synthetic-invalid", "could not scan a local personal-data pattern")]
    public async Task Verify_publication_rejects_invalid_local_patterns_without_echoing_them(string localPatterns, string error)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PathOnlyPolicy, ct);
        await PublicationGuardTestRepository.Stage(repository, "ordinary.txt", "ordinary content", ct);
        repository.WriteText(PublicationGuardTestRepository.LocalPatternsPath, localPatterns);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe($"Publication guard failed: {error}.\n");
        result.StandardOutput.ShouldBeEmpty();
    }

    [Fact]
    public async Task Verify_publication_requires_an_optional_regular_file_when_present()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PathOnlyPolicy, ct);
        Directory.CreateDirectory(Path.Combine(repository.RootPath, PublicationGuardTestRepository.LocalPatternsPath));

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe($"Publication guard failed: {LocalFileError}.\n");
    }

    [Fact]
    public async Task Verify_publication_rejects_local_patterns_above_the_byte_limit()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PathOnlyPolicy, ct);
        repository.WriteText(PublicationGuardTestRepository.LocalPatternsPath, new string('x', (1024 * 1024) + 1));

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("Publication guard failed: the optional local patterns file exceeds 1 MiB.\n");
    }

    [Fact]
    public async Task Verify_publication_requires_utf8_local_patterns()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PathOnlyPolicy, ct);
        repository.WriteBytes(PublicationGuardTestRepository.LocalPatternsPath, [0xFF]);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("Publication guard failed: could not complete the staged check.\n");
    }

    [Fact]
    public async Task Verify_publication_requires_the_policy_as_a_regular_staged_blob()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(ct);
        repository.WriteText("link-target.txt", "ordinary text");
        var target = await repository.StoreBlob("link-target.txt", ct);
        await repository.SetIndexEntry("120000", target, PublicationGuardTestRepository.PolicyPath, ct);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe($"Publication guard failed: {PolicyError}.\n");
    }

    [Fact]
    public async Task Verify_publication_refuses_an_unreadable_git_index()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PathOnlyPolicy, ct);
        repository.CorruptIndex();

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("Publication guard failed: could not inspect staged paths.\n");
    }
}
