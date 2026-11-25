using System.Text;
using static SharedKernel.Secrets.Tool.Tests.SecretsToolHarness;

namespace SharedKernel.Secrets.Tool.Tests;

/// <summary>Tests <c>verify --base</c>, which compares each label's enc with a base revision read through git.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class BaseRevisionTests
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public async Task Files_whose_labels_and_envelopes_equal_the_base_pass_and_git_is_run_without_a_shell_and_with_its_absolute_path()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var content = TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]);
        WriteFile(repository.Path, "secrets/a.sops.yaml", content);
        var runner = ToolScripts.Git(new Dictionary<string, string> { ["secrets/a.sops.yaml"] = content });

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "verify", "--root", repository.Path, "--base", "origin/main");

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldNotContain("changed: needs review", StringComparison.Ordinal);
        runner.Requests.Select(static r => r.Executable).Distinct().ToArray().ShouldBe(["/fake/git"]);
        runner.Requests.Select(static r => string.Join(' ', r.Arguments)).ShouldContain($"-C {Path.GetFullPath(repository.Path)} rev-parse --verify --quiet origin/main^{{commit}}");
        var show = runner.Requests.Where(static r => r.Arguments.Contains("show")).ToArray().ShouldHaveSingleItem();
        show.Arguments.ShouldBe(["-C", Path.GetFullPath(repository.Path), "show", "origin/main:./secrets/a.sops.yaml"]);
    }

    [Fact]
    public async Task A_label_whose_envelope_is_new_or_different_needs_review_and_fails_unless_allowed_by_name()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var current = TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]);
        WriteFile(repository.Path, "secrets/a.sops.yaml", current);
        var runner = ToolScripts.Git(new Dictionary<string, string> { ["secrets/a.sops.yaml"] = TestRecipients.Yaml([TestRecipients.First, TestRecipients.Third]) });

        // Act
        var strict = await Run(repository.Path, runner, ResolveAll, null, null, "verify", "--root", repository.Path, "--base", "HEAD~1");
        var allowed = await Run(repository.Path, runner, ResolveAll, null, null, "verify", "--root", repository.Path, "--base", "HEAD~1", "--allow-changed-recipients");

        // Assert
        strict.ExitCode.ShouldBe(1);
        strict.Output.ShouldContain($"secrets/a.sops.yaml: changed: needs review: recipient {TestRecipients.Second} is new", StringComparison.Ordinal);
        allowed.ExitCode.ShouldBe(0);
        allowed.Output.ShouldContain("changed: needs review", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_label_whose_enc_differs_from_the_base_is_reported_as_changed()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/a.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        var runner = ToolScripts.Git(new Dictionary<string, string> { ["secrets/a.sops.yaml"] = ToolScripts.Rotated(TestRecipients.First) });

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "verify", "--root", repository.Path, "--base", "main");

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain($"recipient {TestRecipients.First} has a different enc", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_that_is_new_since_the_base_or_unreadable_there_has_every_label_changed()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/new.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        WriteFile(repository.Path, "secrets/garbled.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        var runner = ToolScripts.Git(new Dictionary<string, string> { ["secrets/garbled.sops.yaml"] = "not: sops" });

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "verify", "--root", repository.Path, "--base", "main");

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain($"secrets/new.sops.yaml: changed: needs review: recipient {TestRecipients.First} is new", StringComparison.Ordinal);
        run.Output.ShouldContain($"secrets/garbled.sops.yaml: changed: needs review: recipient {TestRecipients.First} is new", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-x")]
    [InlineData("--output=/tmp/x")]
    [InlineData("a:b")]
    [InlineData("a b")]
    [InlineData("a\nb")]
    [InlineData("")]
    public async Task A_revision_that_could_be_an_option_or_a_path_is_refused_before_git_runs(string revision)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/a.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "verify", "--root", repository.Path, "--base", revision);

        // Assert
        run.ExitCode.ShouldBe(2);
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_git_an_unknown_revision_or_a_failing_show_is_a_failure_not_a_pass()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/a.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));

        // Act
        var noGit = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveNone, null, null, "verify", "--root", repository.Path, "--base", "main");
        var unknown = await Run(repository.Path, ToolScripts.Git([], revParseExit: 1), ResolveAll, null, null, "verify", "--root", repository.Path, "--base", "nope");
        var failingShow = await Run(repository.Path, new ScriptedToolRunner(request => request.Arguments.Contains("show") ? ScriptedToolRunner.Result(128, string.Empty, "fatal") : ScriptedToolRunner.Result(0, request.Arguments.Contains("ls-tree") ? "100644 blob abc\tsecrets/a.sops.yaml\0" : "x")), ResolveAll, null, null, "verify", "--root", repository.Path, "--base", "main");

        // Assert
        noGit.ExitCode.ShouldBe(2);
        noGit.Error.ShouldContain("git was not found on PATH.", StringComparison.Ordinal);
        unknown.ExitCode.ShouldBe(2);
        failingShow.ExitCode.ShouldBe(2);
    }

    [Fact]
    public async Task Without_base_no_git_is_run_and_the_allow_flag_alone_is_harmless()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/a.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "verify", "--root", repository.Path, "--allow-changed-recipients");

        // Assert
        run.ExitCode.ShouldBe(0);
        runner.Requests.ShouldBeEmpty();
        Encoding.UTF8.GetByteCount(run.Output).ShouldBeGreaterThan(0);
    }
}
