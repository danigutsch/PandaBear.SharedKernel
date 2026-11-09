using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests <c>verify</c>, which checks every covered file and starts no process.</summary>
[Trait("Capability", "Secrets")]
public sealed class VerifyCommandTests
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public async Task Every_covered_file_that_is_exact_passes_and_uncovered_files_are_counted_not_judged()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/a.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        WriteFile(repository.Path, "secrets/deep/b.sops.json", TestRecipients.Json([TestRecipients.Second, TestRecipients.First]));
        WriteFile(repository.Path, "elsewhere/c.sops.yaml", TestRecipients.Yaml([TestRecipients.Third]));
        WriteFile(repository.Path, "node_modules/x/d.sops.yaml", "ignored");
        WriteFile(repository.Path, ".git/e.sops.yaml", "ignored");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("secrets/a.sops.yaml: ok", StringComparison.Ordinal);
        run.Output.ShouldContain("secrets/deep/b.sops.json: ok", StringComparison.Ordinal);
        run.Output.ShouldContain("2 files match a creation rule: 2 exact, 0 differ, 0 unreadable. 1 other *.sops.* files are not covered by a rule.", StringComparison.Ordinal);
        run.Output.ShouldNotContain("elsewhere", StringComparison.Ordinal);
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
        run.Output.ShouldContain("4 files match a creation rule: 1 exact, 3 differ, 0 unreadable.", StringComparison.Ordinal);
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
        run.Output.ShouldContain("3 files match a creation rule: 0 exact, 1 differ, 2 unreadable.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_symbolic_link_is_not_followed()
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
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("0 files match a creation rule", StringComparison.Ordinal);
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
