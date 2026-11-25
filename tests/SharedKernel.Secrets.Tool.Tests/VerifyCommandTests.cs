using static SharedKernel.Secrets.Tool.Tests.SecretsToolHarness;

namespace SharedKernel.Secrets.Tool.Tests;

/// <summary>Tests <c>verify</c>, which checks every covered file and starts no process.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class VerifyCommandTests
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public async Task Every_covered_file_that_is_exact_passes_and_uncovered_files_are_listed_and_only_allowed_when_asked_for_by_name()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/a.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        WriteFile(repository.Path, "secrets/deep/b.sops.json", TestRecipients.Json([TestRecipients.Second, TestRecipients.First]));
        WriteFile(repository.Path, "elsewhere/c.sops.yaml", TestRecipients.Yaml([TestRecipients.Third]));
        WriteFile(repository.Path, ".git/e.sops.yaml", "ignored");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path, "--allow-unverified-files");

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("secrets/a.sops.yaml: labels consistent", StringComparison.Ordinal);
        run.Output.Split("proves labels and structure, not who can decrypt").Length.ShouldBe(2);
        run.Output.ShouldContain("secrets/deep/b.sops.json: labels consistent", StringComparison.Ordinal);
        run.Output.ShouldContain("elsewhere/c.sops.yaml: no creation rule matches, not verified", StringComparison.Ordinal);
        run.Output.ShouldContain("2 files match a creation rule: 2 consistent, 0 differ, 0 unreadable. 1 other SOPS files are not covered by a rule, 0 entries cannot be judged, 0 have changed recipients.", StringComparison.Ordinal);
        var strict = await Run(repository.Path, "verify", "--root", repository.Path);
        strict.ExitCode.ShouldBe(1);
        strict.Output.ShouldContain("elsewhere/c.sops.yaml: no creation rule matches, not verified", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_subset_a_superset_and_a_different_set_each_make_the_run_exit_with_one()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        WriteFile(repository.Path, "secrets/subset.sops.yaml", TestRecipients.Yaml([TestRecipients.First]));
        WriteFile(repository.Path, "secrets/superset.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second, TestRecipients.Third]));
        WriteFile(repository.Path, "secrets/other.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Third]));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain($"secrets/subset.sops.yaml: missing recipient {TestRecipients.Second}", StringComparison.Ordinal);
        run.Output.ShouldContain($"secrets/superset.sops.yaml: unexpected recipient {TestRecipients.Third}", StringComparison.Ordinal);
        run.Output.ShouldContain($"secrets/other.sops.yaml: missing recipient {TestRecipients.Second}", StringComparison.Ordinal);
        run.Output.ShouldContain($"secrets/other.sops.yaml: unexpected recipient {TestRecipients.Third}", StringComparison.Ordinal);
        run.Output.ShouldContain("4 files match a creation rule: 1 consistent, 3 differ, 0 unreadable.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_covered_file_that_cannot_be_read_is_a_failure_that_outranks_differences()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/subset.sops.yaml", TestRecipients.Yaml([TestRecipients.First]));
        WriteFile(repository.Path, "secrets/broken.sops.yaml", "db: 1\n");
        WriteFile(repository.Path, "secrets/env.sops.env", "A=1");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/broken.sops.yaml:", StringComparison.Ordinal);
        run.Error.ShouldContain("secrets/env.sops.env: Only YAML and JSON", StringComparison.Ordinal);
        run.Output.ShouldContain("3 files match a creation rule: 0 consistent, 1 differ, 2 unreadable.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_symbolic_link_is_not_followed_and_nothing_verified_is_a_failure()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        using var elsewhere = new TemporaryDirectory();
        WriteFile(elsewhere.Path, "x.sops.yaml", TestRecipients.Yaml([TestRecipients.Third]));
        Directory.CreateDirectory(Path.Combine(repository.Path, "secrets"));
        Directory.CreateSymbolicLink(Path.Combine(repository.Path, "secrets", "linked"), elsewhere.Path);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("No file matches a creation rule", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_repository_with_no_encrypted_files_is_a_failure_not_a_pass()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path, "--allow-unverified-files");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("No file matches a creation rule", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("secrets/a:b.sops.yaml")]
    [InlineData("secrets/c\\d.sops.yaml")]
    [InlineData("secrets/new\nline.sops.yaml")]
    public async Task A_file_with_a_path_the_tool_cannot_judge_is_a_failure_even_when_uncovered_files_are_allowed(string file)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        WriteFile(repository.Path, file, TestRecipients.Yaml([TestRecipients.Third]));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path, "--allow-unverified-files");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("cannot be checked", StringComparison.Ordinal);
        run.Output.ShouldContain("1 files match a creation rule: 1 consistent, 0 differ, 0 unreadable.", StringComparison.Ordinal);
        run.Output.ShouldContain("1 entries cannot be judged", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_sops_configuration_file_itself_is_not_an_encrypted_file()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        WriteFile(repository.Path, ".sops.yaml", "creation_rules: []\n");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task A_file_that_hides_the_real_metadata_behind_a_second_document_is_a_failure_not_ok()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var forged = TestRecipients.Yaml([TestRecipients.First, TestRecipients.Third]) + "---\n" + TestRecipients.Block([TestRecipients.First, TestRecipients.Second]);
        WriteFile(repository.Path, "secrets/v1.sops.yaml", forged);
        WriteFile(repository.Path, "secrets/v2.sops.yaml", "\uFEFF" + TestRecipients.Yaml([TestRecipients.First, TestRecipients.Third]));
        WriteFile(repository.Path, "secrets/v3.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Third]).Replace("\nsops:", "\n\"sops\" :", StringComparison.Ordinal) + "---\n" + TestRecipients.Block([TestRecipients.First, TestRecipients.Second]));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Output.ShouldNotContain(": labels consistent", StringComparison.Ordinal);
        run.Output.ShouldContain("3 files match a creation rule: 0 consistent, 0 differ, 3 unreadable.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_with_plaintext_and_a_non_default_encryption_setting_differs_with_the_reasons_named()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/new.sops.yaml", "password: hunter2\n" + TestRecipients.Block([TestRecipients.First, TestRecipients.Second], "    unencrypted_regex: .*\n"));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("secrets/new.sops.yaml: weakened: 1 value is not encrypted", StringComparison.Ordinal);
        run.Output.ShouldContain("secrets/new.sops.yaml: weakened: non-default setting unencrypted_regex", StringComparison.Ordinal);
        run.Output.ShouldNotContain("hunter2", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_root_defaults_to_the_working_directory()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/a.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));

        // Act
        var run = await Run(repository.Path, "verify");

        // Assert
        run.ExitCode.ShouldBe(0);
    }
}
