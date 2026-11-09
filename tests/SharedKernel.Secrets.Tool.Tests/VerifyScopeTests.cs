using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests that <c>verify</c> judges every file that could be encrypted, whatever its name or directory.</summary>
[Trait("Capability", "Secrets")]
public sealed class VerifyScopeTests
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);
    private static readonly string Good = TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]);
    private static readonly string Subset = TestRecipients.Yaml([TestRecipients.First]);

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

    [Theory]
    [InlineData("secrets/prod.yaml")]
    [InlineData("secrets/prod.yml")]
    [InlineData("secrets/prod.enc.yaml")]
    [InlineData("secrets/prod.SOPS.yaml")]
    [InlineData("secrets/PROD.SOPS.YAML")]
    [InlineData("secrets/.sops.yaml")]
    [InlineData("secrets/sub/.sops.yaml")]
    public async Task A_sops_encrypted_file_with_an_unexpected_name_is_judged_like_any_other(string file)
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
    public async Task A_sops_encrypted_json_file_with_an_unexpected_name_is_judged()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "secrets/prod.json", TestRecipients.Json([TestRecipients.First]));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("secrets/prod.json: missing recipient", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Files_in_scope_that_are_not_sops_files_are_ignored_and_the_root_sops_configuration_is_skipped()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "secrets/plain.yaml", "name: value\nlist:\n  - a\n");
        WriteFile(repository.Path, "secrets/data.json", "{\"a\": 1}");
        WriteFile(repository.Path, "secrets/notes.txt", "this file only talks about sops");
        WriteFile(repository.Path, ".sops.yaml", "creation_rules: []\n");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("1 files match a creation rule", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("secrets/flow.yaml")]
    [InlineData("secrets/flow.yml")]
    [InlineData("secrets/token")]
    [InlineData("secrets/store.dat")]
    [InlineData("secrets/deep/binary-store")]
    public async Task A_sops_json_document_is_judged_whatever_its_name_or_whether_it_is_written_on_one_line(string file)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, file, TestRecipients.Json([TestRecipients.First]));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain($"{file}: missing recipient {TestRecipients.Second}", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sops_json_document_with_the_right_recipients_passes_in_any_file_name()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/token", TestRecipients.Json([TestRecipients.First, TestRecipients.Second]));

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("secrets/token: labels consistent", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("indented")]
    [InlineData("escaped")]
    [InlineData("tabbed")]
    public async Task Yaml_the_reader_cannot_fully_understand_but_that_has_a_sops_key_is_judged_and_fails_the_run(string kind)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        var text = kind switch
        {
            "indented" => string.Concat(Subset.Split('\n').Select(static l => l.Length == 0 ? l : "  " + l + "\n")),
            "escaped" => Subset.Replace("\nsops:", "\n\"\\x73ops\":", StringComparison.Ordinal),
            _ => Subset.Replace("\nsops:", "\n\"s\\u006fps\":", StringComparison.Ordinal),
        };
        WriteFile(repository.Path, "secrets/hidden.yaml", text);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/hidden.yaml:", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("utf16")]
    [InlineData("binary")]
    [InlineData("nul")]
    [InlineData("brokenjson")]
    public async Task A_file_in_scope_that_cannot_be_ruled_out_as_non_sops_is_unjudgeable_and_fails_the_run(string kind)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteMystery(Path.Combine(repository.Path, "secrets", "mystery.yaml"), kind, Subset);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/mystery.yaml: the file cannot be ruled out as a SOPS file", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sops_json_document_behind_a_byte_order_mark_is_judged_and_refused()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteMystery(Path.Combine(repository.Path, "secrets", "mystery.yaml"), "jsonwithbom", Subset);

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/mystery.yaml:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Files_in_scope_that_are_positively_not_sops_are_ignored_whatever_their_extension()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        WriteFile(repository.Path, "secrets/README", "We keep secrets with sops. See the sops docs; sops is great.\n");
        WriteFile(repository.Path, "secrets/plain.json", "{\"a\": [1, 2, {\"sops\": 1}]}");
        WriteFile(repository.Path, "secrets/list.json", "[1, 2, 3]");
        WriteFile(repository.Path, "secrets/empty", string.Empty);
        WriteFile(repository.Path, "secrets/nested.yaml", "tools:\n  name: x\n");
        WriteFile(repository.Path, "secrets/data.bin.txt", "just text\n");

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("1 files match a creation rule", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sops_json_document_outside_every_rule_is_uncovered_whatever_its_name()
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
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_fifo_in_scope_does_not_hang_verify_and_is_unjudgeable()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        var fifo = Path.Combine(repository.Path, "secrets", "pipe");
        using var made = await new SystemToolRunner().Run(new ToolRequest("/usr/bin/mkfifo", [fifo]), TestContext.Current.CancellationToken);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        made.ExitCode.ShouldBe(0);
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/pipe:", StringComparison.Ordinal);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task A_file_larger_than_the_limit_in_scope_is_unjudgeable()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/ok.sops.yaml", Good);
        await using (var big = File.Create(Path.Combine(repository.Path, "secrets", "big.yaml")))
        {
            big.SetLength((16 * 1024 * 1024) + 1);
        }

        // Act
        var run = await Run(repository.Path, "verify", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("secrets/big.yaml: the file is larger than 16 MiB", StringComparison.Ordinal);
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
