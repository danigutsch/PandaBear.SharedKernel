using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests that <c>verify</c> judges every file that could be encrypted, whatever its name or directory.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class VerifyScopeTests
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);
    private static readonly string Good = TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]);
    private static readonly string Subset = TestRecipients.Yaml([TestRecipients.First]);

    [Theory]
    [InlineData("secrets/prod.yaml")]
    [InlineData("secrets/prod.yml")]
    [InlineData("secrets/prod.enc.yaml")]
    [InlineData("secrets/token")]
    [InlineData("secrets/store.dat")]
    [InlineData("secrets/flow.yaml")]
    [InlineData("secrets/deep/binary-store")]
    [InlineData("secrets/README")]
    [InlineData("secrets/plain.json")]
    [InlineData("secrets/.gitkeep")]
    [InlineData("secrets/empty")]
    public async Task Every_file_in_the_scope_of_a_rule_whose_name_does_not_mark_it_as_sops_is_rejected_whatever_it_holds(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, file, file.EndsWith("empty", StringComparison.Ordinal) ? string.Empty : file.Contains("flow", StringComparison.Ordinal) ? TestRecipients.Json([TestRecipients.First]) : Subset);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain($"{file}: not judged: name does not mark it as SOPS-encrypted (rename it to *.sops.<ext>, or move it out of the rule's scope)", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("secrets/prod.SOPS.yaml")]
    [InlineData("secrets/PROD.SOPS.YAML")]
    [InlineData("secrets/.sops.yaml")]
    [InlineData("secrets/sub/.sops.yaml")]
    public async Task A_file_named_sops_in_any_letter_case_is_judged_where_it_sits(string file)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, file, Subset);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain($"{file}: missing recipient {TestRecipients.Second}", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sops_JSON_document_named_sops_passes_with_the_right_recipients_and_the_root_configuration_is_skipped()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/token.sops.json", TestRecipients.Json([TestRecipients.First, TestRecipients.Second]));
        WriteFile(repository.Path, ".sops.yaml", "creation_rules: []\n");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("secrets/token.sops.json: labels consistent", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("indented")]
    [InlineData("escaped")]
    [InlineData("tabbed")]
    [InlineData("complexkey")]
    [InlineData("tag")]
    [InlineData("anchor")]
    [InlineData("trailingtab")]
    [InlineData("binarytag")]
    [InlineData("verbatimtag")]
    [InlineData("tagdirective")]
    public async Task Yaml_named_sops_that_the_reader_cannot_fully_understand_is_never_ok(string kind)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        var text = kind switch
        {
            "indented" => string.Concat(Subset.Split('\n').Select(static l => l.Length == 0 ? l : "  " + l + "\n")),
            "escaped" => Subset.Replace("\nsops:", "\n\"\\x73ops\":", StringComparison.Ordinal),
            "tabbed" => Subset.Replace("\nsops:", "\n\"s\\u006fps\":", StringComparison.Ordinal),
            "complexkey" => Subset.Replace("sops:\n", "? sops\n:\n", StringComparison.Ordinal),
            "tag" => Subset.Replace("sops:\n", "!!str sops:\n", StringComparison.Ordinal),
            "anchor" => Subset.Replace("sops:\n", "&x sops:\n", StringComparison.Ordinal),
            "trailingtab" => Subset.Replace("sops:\n", "sops:\t\n", StringComparison.Ordinal),
            "binarytag" => Subset.Replace("\nsops:", "\n!!binary c29wcw==:", StringComparison.Ordinal),
            "verbatimtag" => Subset.Replace("\nsops:", "\n!<tag:yaml.org,2002:binary> c29wcw==:", StringComparison.Ordinal),
            _ => "%TAG !x! tag:yaml.org,2002:bi\n---\n" + Subset.Replace("\nsops:", "\n!x!n%61ry c29wcw==:", StringComparison.Ordinal),
        };
        WriteFile(repository.Path, "secrets/hidden.sops.yaml", text);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/hidden.sops.yaml:", StringComparison.Ordinal);
        run.Output.ShouldNotContain("secrets/hidden.sops.yaml: labels consistent", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("utf16")]
    [InlineData("binary")]
    [InlineData("nul")]
    [InlineData("brokenjson")]
    public async Task A_file_named_sops_that_is_not_readable_metadata_is_unjudgeable_and_fails_the_run(string kind)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteMystery(Path.Combine(repository.Path, "secrets", "mystery.sops.yaml"), kind, Subset);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/mystery.sops.yaml:", StringComparison.Ordinal);
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_fifo_in_scope_does_not_hang_verify_whether_or_not_its_name_marks_it_as_sops()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        using var plain = await new SystemToolRunner().Run(new ToolRequest("/usr/bin/mkfifo", [Path.Combine(repository.Path, "secrets", "pipe")]), TestContext.Current.CancellationToken);
        using var named = await new SystemToolRunner().Run(new ToolRequest("/usr/bin/mkfifo", [Path.Combine(repository.Path, "secrets", "pipe.sops.yaml")]), TestContext.Current.CancellationToken);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        plain.ExitCode.ShouldBe(0);
        named.ExitCode.ShouldBe(0);
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/pipe: not judged", StringComparison.Ordinal);
        run.Error.ShouldContain("secrets/pipe.sops.yaml:", StringComparison.Ordinal);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task A_file_named_sops_larger_than_the_limit_is_unjudgeable()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        await using (var big = File.Create(Path.Combine(repository.Path, "secrets", "big.sops.yaml")))
        {
            big.SetLength((16 * 1024 * 1024) + 1);
        }

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/big.sops.yaml: the file is larger than 16 MiB", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Outside_every_rule_a_file_that_looks_like_sops_is_listed_as_uncovered_on_a_best_effort_basis_and_one_that_does_not_is_ignored()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "other/prod.yaml", Subset);
        WriteFile(repository.Path, "other/flow.yaml", TestRecipients.Json([TestRecipients.First]));
        WriteFile(repository.Path, "other/plain.yaml", "name: value\n");
        WriteFile(repository.Path, "other/notes.txt", "sops everywhere");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("other/prod.yaml: no creation rule matches, not verified", StringComparison.Ordinal);
        run.Output.ShouldContain("other/flow.yaml: no creation rule matches, not verified", StringComparison.Ordinal);
        run.Output.ShouldNotContain("other/plain.yaml", StringComparison.Ordinal);
        run.Output.ShouldNotContain("other/notes.txt", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("secrets/bin/a.sops.yaml")]
    [InlineData("secrets/obj/a.sops.yaml")]
    [InlineData("secrets/node_modules/a.sops.yaml")]
    [InlineData("secrets/deep/node_modules/x/a.sops.yaml")]
    public async Task Directories_named_like_build_output_are_not_skipped_inside_the_scope_of_a_rule(string file)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, file, Subset);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain($"{file}: missing recipient", StringComparison.Ordinal);
    }


    [Fact]
    public async Task Only_the_git_directory_is_skipped()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "secrets/.git/hidden.sops.yaml", Subset);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
    }


    [Fact]
    public async Task A_file_named_sops_in_any_letter_case_is_judged_even_when_its_extension_is_not_a_known_one()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "secrets/blob.SOPS.bin", "binary");
        WriteFile(repository.Path, "other/blob.Sops.dat", "binary");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/blob.SOPS.bin: Only YAML and JSON", StringComparison.Ordinal);
        run.Output.ShouldContain("other/blob.Sops.dat: no creation rule matches, not verified", StringComparison.Ordinal);
    }


    [Fact]
    public async Task A_sops_looking_file_outside_every_rule_with_a_sops_name_is_listed_not_skipped_even_when_it_cannot_be_parsed()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "other/x.sops.yaml", "? sops\n:\n");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("other/x.sops.yaml: no creation rule matches, not verified", StringComparison.Ordinal);
    }


    [Fact]
    public async Task A_sops_JSON_document_outside_every_rule_is_uncovered_whatever_its_name()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "other/flow.yaml", TestRecipients.Json([TestRecipients.First]));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("other/flow.yaml: no creation rule matches, not verified", StringComparison.Ordinal);
    }


    [Fact]
    public async Task A_symbolic_link_to_a_directory_inside_the_scope_is_unjudgeable_because_sops_would_decrypt_what_is_behind_it()
    {
        // Arrange
        using var repository = CreateRepository(TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second).Replace("^secrets/", "^secrets/.*\\\\.sops\\\\.yaml$", StringComparison.Ordinal));
        using var elsewhere = new TemporaryDirectory();
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(elsewhere.Path, "evil.sops.yaml", Subset);
        Directory.CreateSymbolicLink(Path.Combine(repository.Path, "secrets", "prod"), elsewhere.Path);
        File.CreateSymbolicLink(Path.Combine(repository.Path, "secrets", "evil.sops.yml"), Path.Combine(elsewhere.Path, "evil.sops.yaml"));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/prod: a symbolic link cannot be checked", StringComparison.Ordinal);
        run.Error.ShouldContain("secrets/evil.sops.yml: a symbolic link cannot be checked", StringComparison.Ordinal);
    }


    [Fact]
    public async Task A_symbolic_link_named_like_a_sops_file_outside_every_rule_is_listed_as_uncovered_and_one_with_another_name_is_ignored()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        using var elsewhere = new TemporaryDirectory();
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(elsewhere.Path, "real.sops.yaml", Subset);
        Directory.CreateDirectory(Path.Combine(repository.Path, "other"));
        File.CreateSymbolicLink(Path.Combine(repository.Path, "other", "link.sops.yaml"), Path.Combine(elsewhere.Path, "real.sops.yaml"));
        File.CreateSymbolicLink(Path.Combine(repository.Path, "other", "plain-link"), Path.Combine(elsewhere.Path, "real.sops.yaml"));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("other/link.sops.yaml: no creation rule matches, not verified", StringComparison.Ordinal);
        run.Output.ShouldNotContain("plain-link", StringComparison.Ordinal);
    }


    [Fact]
    public async Task A_sops_encrypted_file_outside_every_rule_is_listed_as_uncovered_whatever_its_name()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "other/prod.yaml", Subset);
        WriteFile(repository.Path, "other/Config.SOPS.json", TestRecipients.Json([TestRecipients.First]));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);
        var allowed = await Run(repository.Path, "verify", "--root", repository.Path, "--allow-unverified-files");

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("other/prod.yaml: no creation rule matches, not verified", StringComparison.Ordinal);
        run.Output.ShouldContain("other/Config.SOPS.json: no creation rule matches, not verified", StringComparison.Ordinal);
        allowed.ExitCode.ShouldBe(0);
    }


    [Fact]
    public async Task A_symbolic_link_in_scope_is_unjudgeable_and_one_outside_every_rule_is_ignored()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        using var elsewhere = new TemporaryDirectory();
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(elsewhere.Path, "real.sops.yaml", Subset);
        File.CreateSymbolicLink(Path.Combine(repository.Path, "secrets", "link.sops.yaml"), Path.Combine(elsewhere.Path, "real.sops.yaml"));
        Directory.CreateDirectory(Path.Combine(repository.Path, "other"));
        File.CreateSymbolicLink(Path.Combine(repository.Path, "other", "link.sops.yaml"), Path.Combine(elsewhere.Path, "real.sops.yaml"));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/link.sops.yaml: a symbolic link cannot be checked", StringComparison.Ordinal);
        run.Error.ShouldNotContain("other/link", StringComparison.Ordinal);
    }


    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task An_unreadable_file_or_directory_in_scope_is_unjudgeable_and_exit_two()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "secrets/locked.yaml", Subset);
        WriteFile(repository.Path, "secrets/closed/x.sops.yaml", Subset);
        File.SetUnixFileMode(Path.Combine(repository.Path, "secrets", "locked.yaml"), UnixFileMode.None);
        File.SetUnixFileMode(Path.Combine(repository.Path, "secrets", "closed"), UnixFileMode.None);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);
        File.SetUnixFileMode(Path.Combine(repository.Path, "secrets", "closed"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/locked.yaml:", StringComparison.Ordinal);
        run.Error.ShouldContain("secrets/closed:", StringComparison.Ordinal);
    }
}
