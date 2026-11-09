using static PandaBear.Sandbox.Tool.Tests.SettingsToolHarness;

namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Tests the <c>settings plan</c>, <c>apply</c>, and <c>verify</c> commands.</summary>
[Trait("Capability", "Sandbox")]
public sealed class SettingsCommandTests
{
    private const string Dotnet = """{ "profile": "dotnet" }""";

    [Fact]
    public async Task Plan_lists_the_additions_and_writes_nothing()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet));

        // Act
        var (exitCode, output, error) = await Run(repository.Path, "plan");

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldContain("add $.sandbox.network.allowedDomains: api.nuget.org", StringComparison.Ordinal);
        output.ShouldContain("add $.env: MSBUILDDISABLENODEREUSE", StringComparison.Ordinal);
        output.ShouldContain("Preview only; nothing was written.", StringComparison.Ordinal);
        Directory.Exists(Path.Combine(repository.Path, ".claude")).ShouldBeFalse();
    }

    [Fact]
    public async Task Apply_without_force_only_previews()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet), "{}");

        // Act
        var (exitCode, output, _) = await Run(repository.Path, "apply");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("Pass --force to write.", StringComparison.Ordinal);
        ReadText(Path.Combine(repository.Path, DefaultTarget)).ShouldBe("{}");
    }

    [Fact]
    public async Task Forced_apply_creates_the_file_and_a_second_plan_is_empty()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet));

        // Act
        var (exitCode, output, _) = await Run(repository.Path, "apply", "--force");
        var second = await Run(repository.Path, "plan");
        var verify = await Run(repository.Path, "verify");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("Wrote 4 changes to .claude/settings.json.", StringComparison.Ordinal);
        List(repository.Path, DefaultTarget, "sandbox", "network", "allowedDomains").ShouldBe(["api.nuget.org"]);
        second.Output.ShouldContain("Up to date: nothing to change.", StringComparison.Ordinal);
        verify.ExitCode.ShouldBe(0);
        Siblings(repository.Path, DefaultTarget).ShouldBe(["settings.json"]);
    }

    [Fact]
    public async Task Unknown_keys_and_hand_added_entries_are_preserved_in_order()
    {
        // Arrange
        const string Existing = """
            {
              "model": "x",
              "permissions": { "allow": ["Bash(ls)"] },
              "sandbox": {
                "enabled": true,
                "filesystem": { "allowWrite": ["~/.kube", "/tmp/build"], "other": 1 },
                "network": { "allowedDomains": ["example.com"] }
              },
              "env": { "KEEP": "1" }
            }
            """;
        using var repository = CreateRepository(Configuration("""{ "profile": "dotnet", "writeNuGetFolders": true }"""), Existing);

        // Act
        var (exitCode, _, _) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(0);
        var text = ReadText(Path.Combine(repository.Path, DefaultTarget));
        text.ShouldContain("\"model\": \"x\"", StringComparison.Ordinal);
        text.ShouldContain("\"other\": 1", StringComparison.Ordinal);
        text.ShouldContain("\"KEEP\": \"1\"", StringComparison.Ordinal);
        text.IndexOf("\"model\"", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("\"sandbox\"", StringComparison.Ordinal));
        List(repository.Path, DefaultTarget, "sandbox", "filesystem", "allowWrite")
            .ShouldBe(["~/.kube", "/tmp/build", "~/.nuget/packages", "~/.local/share/NuGet"]);
        List(repository.Path, DefaultTarget, "sandbox", "network", "allowedDomains").ShouldBe(["example.com", "api.nuget.org"]);
        List(repository.Path, DefaultTarget, "permissions", "allow").ShouldBe(["Bash(ls)"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n")]
    [InlineData("{}")]
    public async Task An_empty_settings_file_gets_the_managed_section(string existing)
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet), existing);

        // Act
        var (exitCode, _, _) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(0);
        List(repository.Path, DefaultTarget, "sandbox", "network", "allowedDomains").ShouldBe(["api.nuget.org"]);
    }

    [Theory]
    [InlineData("    ", "\n")]
    [InlineData("\t", "\n")]
    [InlineData("  ", "\r\n")]
    public async Task Indentation_and_line_endings_of_the_existing_file_are_kept(string indent, string newLine)
    {
        // Arrange
        var existing = "{" + newLine + indent + "\"a\": 1" + newLine + "}" + newLine;
        using var repository = CreateRepository(Configuration("""{ "allowedDomains": ["example.com"] }"""), existing);

        // Act
        var (exitCode, _, _) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(0);
        var text = ReadText(Path.Combine(repository.Path, DefaultTarget));
        text.ShouldStartWith("{" + newLine + indent + "\"a\": 1,", StringComparison.Ordinal);
        text.ShouldEndWith("}" + newLine, StringComparison.Ordinal);
        text.Replace(newLine, string.Empty, StringComparison.Ordinal).ShouldNotContain("\n", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_trailing_slash_does_not_make_an_entry_missing()
    {
        // Arrange
        const string Existing = """{ "sandbox": { "filesystem": { "allowWrite": ["~/.nuget/packages/"] } } }""";
        using var repository = CreateRepository(Configuration("""{ "allowWrite": ["~/.nuget/packages"] }"""), Existing);

        // Act
        var (exitCode, output, _) = await Run(repository.Path, "verify");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("No drift", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Denied_read_paths_become_deny_read_entries()
    {
        // Arrange
        using var repository = CreateRepository(Configuration("{}", """["~/.ssh"]"""), "{}");

        // Act
        var (exitCode, _, _) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(0);
        List(repository.Path, DefaultTarget, "sandbox", "filesystem", "denyRead").ShouldBe(["~/.ssh"]);
    }

    [Fact]
    public async Task Profile_opt_ins_add_their_entries_only_when_asked()
    {
        // Arrange
        using var plain = CreateRepository(Configuration(Dotnet));
        using var opted = CreateRepository(Configuration("""{ "profile": "dotnet", "writeNuGetFolders": true, "writeDotnetHome": true, "allowSdkDownload": true, "environment": false }"""));

        // Act
        var plainResult = await Run(plain.Path, "plan");
        var optedResult = await Run(opted.Path, "plan");

        // Assert
        plainResult.Output.ShouldNotContain("allowWrite", StringComparison.Ordinal);
        plainResult.Output.ShouldNotContain("builds.dotnet.microsoft.com", StringComparison.Ordinal);
        optedResult.Output.ShouldContain("allowWrite: ~/.dotnet", StringComparison.Ordinal);
        optedResult.Output.ShouldContain("allowWrite: ~/.nuget/packages", StringComparison.Ordinal);
        optedResult.Output.ShouldContain("allowedDomains: builds.dotnet.microsoft.com", StringComparison.Ordinal);
        optedResult.Output.ShouldNotContain("$.env", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_different_environment_value_is_kept_and_warned_about()
    {
        // Arrange
        const string Existing = """{ "env": { "DOTNET_CLI_TELEMETRY_OPTOUT": "0" } }""";
        using var repository = CreateRepository(Configuration(Dotnet), Existing);

        // Act
        var (exitCode, output, _) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("warning: $.env.DOTNET_CLI_TELEMETRY_OPTOUT has a different value", StringComparison.Ordinal);
        ReadText(Path.Combine(repository.Path, DefaultTarget)).ShouldContain("\"DOTNET_CLI_TELEMETRY_OPTOUT\": \"0\"", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_existing_allow_all_unix_sockets_is_reported_but_left_alone()
    {
        // Arrange
        const string Existing = """{ "sandbox": { "network": { "allowAllUnixSockets": true } } }""";
        using var repository = CreateRepository(Configuration("{}"), Existing);

        // Act
        var (exitCode, output, _) = await Run(repository.Path, "verify");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("warning: $.sandbox.network.allowAllUnixSockets is true", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "allowAllUnixSockets": true }""", "container engine", "--accept-risk-allow-all-unix-sockets")]
    [InlineData("""{ "excludedCommands": ["docker *"] }""", "outside the sandbox", "--accept-risk-excluded-commands")]
    [InlineData("""{ "removeDenyRead": ["~/.ssh"] }""", "credential folders", "--accept-risk-remove-deny-read")]
    public async Task A_risky_request_is_refused_only_when_forced_and_names_the_exposure(string settings, string exposure, string flag)
    {
        // Arrange
        using var repository = CreateRepository(Configuration(settings, """["~/.gnupg"]"""), """{ "sandbox": { "filesystem": { "denyRead": ["~/.ssh"] } } }""");

        // Act
        var plan = await Run(repository.Path, "plan");
        var verify = await Run(repository.Path, "verify");
        var preview = await Run(repository.Path, "apply");
        var apply = await Run(repository.Path, "apply", "--force");

        // Assert
        plan.ExitCode.ShouldBe(0);
        plan.Output.ShouldContain("would require " + flag, StringComparison.Ordinal);
        verify.ExitCode.ShouldBe(1);
        verify.Output.ShouldContain("would require " + flag, StringComparison.Ordinal);
        preview.ExitCode.ShouldBe(0);
        preview.Output.ShouldContain("would require " + flag, StringComparison.Ordinal);
        apply.ExitCode.ShouldBe(2);
        apply.Error.ShouldContain("Refused:", StringComparison.Ordinal);
        apply.Error.ShouldContain(exposure, StringComparison.Ordinal);
        apply.Error.ShouldContain(flag, StringComparison.Ordinal);
        ReadText(Path.Combine(repository.Path, DefaultTarget)).ShouldContain("~/.ssh", StringComparison.Ordinal);
        ReadText(Path.Combine(repository.Path, DefaultTarget)).ShouldNotContain("~/.gnupg", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/home")]
    [InlineData("/home/")]
    [InlineData("/home/user")]
    [InlineData("/home//user/.")]
    [InlineData("/etc")]
    [InlineData("/usr/")]
    public async Task A_broad_write_path_is_refused_when_forced_without_its_flag(string entry)
    {
        // Arrange
        using var repository = CreateRepository(Configuration($$"""{ "allowWrite": ["{{entry}}"] }"""));

        // Act
        var plan = await Run(repository.Path, "plan");
        var refused = await Run(repository.Path, "apply", "--force");
        var accepted = await Run(repository.Path, "apply", "--force", "--accept-risk-broad-write", "--accept-risk-sensitive-write");

        // Assert
        plan.ExitCode.ShouldBe(0);
        plan.Output.ShouldContain("would require --accept-risk-broad-write", StringComparison.Ordinal);
        refused.ExitCode.ShouldBe(2);
        refused.Error.ShouldContain("$.settings.allowWrite[0]", StringComparison.Ordinal);
        refused.Error.ShouldContain("--accept-risk-broad-write", StringComparison.Ordinal);
        accepted.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task The_account_home_folder_is_refused_even_when_home_points_elsewhere()
    {
        // Arrange
        using var repository = CreateRepository(Configuration("""{ "allowWrite": ["/home/real"] }"""));
        var environment = new FakeSandboxEnvironment { HomeDirectory = "/x", AccountHomeDirectory = "/home/real" };

        // Act
        var (exitCode, _, error) = await Run(repository.Path, environment, null, "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("--accept-risk-broad-write", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_symlink_to_the_home_folder_is_refused()
    {
        // Arrange
        using var home = new TemporaryDirectory();
        using var elsewhere = new TemporaryDirectory();
        var link = Path.Combine(elsewhere.Path, "link");
        Directory.CreateSymbolicLink(link, home.Path);
        using var repository = CreateRepository(Configuration($$"""{ "allowWrite": ["{{link}}"] }"""));
        var environment = new FakeSandboxEnvironment { HomeDirectory = home.Path };

        // Act
        var (exitCode, _, error) = await Run(repository.Path, environment, null, "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("--accept-risk-broad-write", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_real_folder_behind_a_home_that_is_a_symlink_is_refused()
    {
        // Arrange
        using var real = new TemporaryDirectory();
        using var elsewhere = new TemporaryDirectory();
        var link = Path.Combine(elsewhere.Path, "home");
        Directory.CreateSymbolicLink(link, real.Path);
        using var repository = CreateRepository(Configuration($$"""{ "allowWrite": ["{{real.Path}}"] }"""));
        var environment = new FakeSandboxEnvironment { HomeDirectory = link };

        // Act
        var (exitCode, _, error) = await Run(repository.Path, environment, null, "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("--accept-risk-broad-write", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("~/.bashrc")]
    [InlineData("~/.zshrc")]
    [InlineData("~/.profile")]
    [InlineData("~/.bash_profile")]
    [InlineData("~/.ssh")]
    [InlineData("~/.ssh/config")]
    [InlineData("~/.gnupg")]
    [InlineData("~/.config/autostart")]
    [InlineData("~/.config")]
    [InlineData("/home/user/.bashrc")]
    [InlineData("./.git/hooks")]
    [InlineData("./.git/config")]
    [InlineData("./.git")]
    [InlineData("./.git/hooks/pre-commit")]
    public async Task A_sensitive_write_path_is_refused_when_forced_without_its_flag(string entry)
    {
        // Arrange
        using var repository = CreateRepository(Configuration($$"""{ "allowWrite": ["{{entry}}"] }"""));

        // Act
        var refused = await Run(repository.Path, "apply", "--force");
        var accepted = await Run(repository.Path, "apply", "--force", "--accept-risk-sensitive-write");

        // Assert
        refused.ExitCode.ShouldBe(2);
        refused.Error.ShouldContain("--accept-risk-sensitive-write", StringComparison.Ordinal);
        accepted.ExitCode.ShouldBe(0);
    }

    [Theory]
    [InlineData("~/.config/other")]
    [InlineData("~/.cache/tool")]
    [InlineData("./build")]
    [InlineData("/tmp/build")]
    [InlineData("./.github")]
    public async Task An_ordinary_write_path_needs_no_flag(string entry)
    {
        // Arrange
        using var repository = CreateRepository(Configuration($$"""{ "allowWrite": ["{{entry}}"] }"""));

        // Act
        var (exitCode, output, _) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldNotContain("would require", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_risk_flag_lets_its_request_through()
    {
        // Arrange
        using var repository = CreateRepository(
            Configuration("""{ "allowAllUnixSockets": true, "excludedCommands": ["docker *"], "removeDenyRead": ["~/.ssh"] }"""),
            """{ "sandbox": { "filesystem": { "denyRead": ["~/.ssh", "~/.aws"] } } }""");

        // Act
        var (exitCode, output, _) = await Run(
            repository.Path,
            "apply",
            "--force",
            "--accept-risk-allow-all-unix-sockets",
            "--accept-risk-excluded-commands",
            "--accept-risk-remove-deny-read");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("set $.sandbox.network.allowAllUnixSockets: true", StringComparison.Ordinal);
        output.ShouldContain("remove $.sandbox.filesystem.denyRead: ~/.ssh", StringComparison.Ordinal);
        List(repository.Path, DefaultTarget, "sandbox", "excludedCommands").ShouldBe(["docker *"]);
        List(repository.Path, DefaultTarget, "sandbox", "filesystem", "denyRead").ShouldBe(["~/.aws"]);
    }

    [Fact]
    public async Task Removing_a_path_that_the_configuration_requires_is_an_error_even_with_the_flag()
    {
        // Arrange
        using var repository = CreateRepository(Configuration("""{ "removeDenyRead": ["~/.ssh"] }""", """["~/.ssh"]"""));

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "plan", "--accept-risk-remove-deny-read");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("$.settings.removeDenyRead names a path that", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Removing_a_required_path_spelled_with_a_trailing_slash_is_still_an_error()
    {
        // Arrange
        using var repository = CreateRepository(Configuration("""{ "removeDenyRead": ["~/.ssh/"] }""", """["~/.ssh"]"""), """{ "sandbox": { "filesystem": { "denyRead": ["~/.ssh"] } } }""");

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "apply", "--force", "--accept-risk-remove-deny-read");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("requires", StringComparison.Ordinal);
        ReadText(Path.Combine(repository.Path, DefaultTarget)).ShouldContain("~/.ssh", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_lone_surrogate_escape_in_the_settings_file_names_the_file()
    {
        // Arrange
        using var repository = CreateRepository(Configuration("{}"), """{ "sandbox": { "filesystem": { "allowWrite": ["\ud800"] } } }""");

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "verify");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain(".claude/settings.json", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_missing_managed_entries_as_drift_and_ignores_hand_added_ones()
    {
        // Arrange
        const string Existing = """{ "sandbox": { "filesystem": { "allowWrite": ["~/mine"] }, "network": { "allowedDomains": ["mine.example"] } } }""";
        using var repository = CreateRepository(Configuration("""{ "allowWrite": ["~/managed"] }"""), Existing);

        // Act
        var (exitCode, output, _) = await Run(repository.Path, "verify");

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldContain("add $.sandbox.filesystem.allowWrite: ~/managed", StringComparison.Ordinal);
        output.ShouldNotContain("~/mine", StringComparison.Ordinal);
        output.ShouldNotContain("mine.example", StringComparison.Ordinal);
        output.ShouldContain("Drift: 1 managed entries differ.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_never_writes()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet));

        // Act
        var (exitCode, _, _) = await Run(repository.Path, "verify");

        // Assert
        exitCode.ShouldBe(1);
        Directory.Exists(Path.Combine(repository.Path, ".claude")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_failure_before_the_rename_leaves_the_original_and_no_temporary_file()
    {
        // Arrange
        const string Existing = """{ "a": 1 }""";
        using var repository = CreateRepository(Configuration(Dotnet), Existing);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, () => throw new IOException("disk full"), "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("disk full", StringComparison.Ordinal);
        ReadText(Path.Combine(repository.Path, DefaultTarget)).ShouldBe(Existing);
        Siblings(repository.Path, DefaultTarget).ShouldBe(["settings.json"]);
    }

    [Fact]
    public async Task A_failure_removes_a_directory_that_the_run_created()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet));

        // Act
        var (exitCode, _, _) = await Run(repository.Path, () => throw new IOException("disk full"), "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        Directory.Exists(Path.Combine(repository.Path, ".claude")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_file_that_changed_after_the_read_is_not_overwritten()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet), "{}");
        var path = Path.Combine(repository.Path, DefaultTarget);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, () => WriteText(path, """{ "hand": "edit" }"""), "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("changed after the plan was made", StringComparison.Ordinal);
        ReadText(path).ShouldBe("""{ "hand": "edit" }""");
    }

    [Theory]
    [InlineData("{ ", "$")]
    [InlineData("""{ "sandbox": {}, "sandbox": {} }""", "$")]
    [InlineData("""{ /* c */ }""", "$")]
    [InlineData("[]", "$ must be a JSON object")]
    [InlineData("""{ "sandbox": [] }""", "$.sandbox must be an object")]
    [InlineData("""{ "sandbox": { "filesystem": { "allowWrite": "x" } } }""", "$.sandbox.filesystem.allowWrite must be a list of strings")]
    [InlineData("""{ "sandbox": { "filesystem": { "allowWrite": ["a", 1] } } }""", "$.sandbox.filesystem.allowWrite[1] must be a string")]
    [InlineData("""{ "env": [] }""", "$.env must be an object")]
    public async Task An_invalid_settings_file_fails_with_exit_two_naming_the_file_and_the_path(string existing, string expected)
    {
        // Arrange
        using var repository = CreateRepository(Configuration("""{ "profile": "dotnet", "allowWrite": ["~/x"] }"""), existing);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain(".claude/settings.json", StringComparison.Ordinal);
        error.ShouldContain(expected, StringComparison.Ordinal);
        ReadText(Path.Combine(repository.Path, DefaultTarget)).ShouldBe(existing);
    }

    [Fact]
    public async Task A_settings_file_larger_than_one_mebibyte_is_refused()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet), "{ \"x\": \"" + new string('a', 1024 * 1024) + "\" }");

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "plan");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("larger than 1 MiB", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_settings_file_that_is_not_utf8_is_refused()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet), "{}");
        File.WriteAllBytes(Path.Combine(repository.Path, DefaultTarget), [0x7B, 0xFF, 0x7D]);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "plan");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("not valid UTF-8", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_byte_order_mark_is_read_but_not_kept()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet), "{}");
        File.WriteAllBytes(Path.Combine(repository.Path, DefaultTarget), [0xEF, 0xBB, 0xBF, 0x7B, 0x7D]);

        // Act
        var (exitCode, _, _) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(0);
        ReadBytes(Path.Combine(repository.Path, DefaultTarget))[0].ShouldBe((byte)'{');
    }

    [Fact]
    public async Task A_symlinked_directory_that_leaves_the_root_is_never_followed()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet));
        using var outside = new TemporaryDirectory();
        Directory.CreateSymbolicLink(Path.Combine(repository.Path, ".claude"), outside.Path);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("Symbolic links are not followed", StringComparison.Ordinal);
        Directory.GetFileSystemEntries(outside.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_symlinked_settings_file_is_never_followed()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet), "{}");
        using var outside = new TemporaryDirectory();
        var secret = Path.Combine(outside.Path, "other.json");
        WriteText(secret, "{}");
        var link = Path.Combine(repository.Path, DefaultTarget);
        File.Delete(link);
        File.CreateSymbolicLink(link, secret);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("Symbolic links are not followed", StringComparison.Ordinal);
        ReadText(secret).ShouldBe("{}");
    }

    [Theory]
    [InlineData("""{ "target": "../outside.json" }""", "$.settings.target")]
    [InlineData("""{ "target": "/etc/x.json" }""", "$.settings.target")]
    [InlineData("""{ "target": ".claude/settings.txt" }""", "$.settings.target")]
    [InlineData("""{ "profile": "ruby" }""", "$.settings.profile")]
    [InlineData("""{ "writeNuGetFolders": true }""", "need \"profile\": \"dotnet\"")]
    [InlineData("""{ "allowWrite": ["~/"] }""", "$.settings.allowWrite[0] would make the whole")]
    [InlineData("""{ "allowWrite": ["/"] }""", "$.settings.allowWrite[0] would make the whole")]
    [InlineData("""{ "allowWrite": ["/."] }""", "$.settings.allowWrite[0] would make the whole")]
    [InlineData("""{ "allowWrite": ["//"] }""", "$.settings.allowWrite[0] would make the whole")]
    [InlineData("""{ "allowWrite": ["/./"] }""", "$.settings.allowWrite[0] would make the whole")]
    [InlineData("""{ "allowWrite": ["~/."] }""", "$.settings.allowWrite[0] would make the whole")]
    [InlineData("""{ "allowWrite": ["~//./"] }""", "$.settings.allowWrite[0] would make the whole")]
    [InlineData("""{ "allowedDomains": ["*.com"] }""", "$.settings.allowedDomains[0]")]
    [InlineData("""{ "allowedDomains": ["*.co.uk"] }""", "$.settings.allowedDomains[0]")]
    [InlineData("""{ "allowedDomains": ["*.com:443"] }""", "$.settings.allowedDomains[0]")]
    [InlineData("""{ "allowWrite": ["build"] }""", "$.settings.allowWrite[0] must be an absolute path")]
    [InlineData("""{ "denyWrite": ["~/a/../b"] }""", "$.settings.denyWrite[0]")]
    [InlineData("""{ "allowedDomains": ["https://x.example"] }""", "$.settings.allowedDomains[0] must be a host name")]
    [InlineData("""{ "allowedDomains": ["a b"] }""", "$.settings.allowedDomains[0]")]
    [InlineData("""{ "excludedCommands": [""] }""", "$.settings.excludedCommands[0]")]
    [InlineData("""{ "denyRead": ["~/a\u0007"] }""", "$.settings.denyRead[0]")]
    [InlineData("""{ "bogus": 1 }""", "$.settings.bogus")]
    public async Task An_invalid_settings_section_fails_with_exit_two_naming_the_json_path(string settings, string expected)
    {
        // Arrange
        using var repository = CreateRepository(Configuration(settings));

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "plan");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain(".config/sharedkernel-sandbox.json", StringComparison.Ordinal);
        error.ShouldContain(expected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Too_many_entries_in_a_list_fail()
    {
        // Arrange
        var entries = string.Join(", ", Enumerable.Range(0, 101).Select(static index => $"\"~/p{index}\""));
        using var repository = CreateRepository(Configuration($$"""{ "denyWrite": [{{entries}}] }"""));

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "plan");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("$.settings.denyWrite must have at most 100 entries", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_configuration_without_a_settings_section_or_without_the_file_fails()
    {
        // Arrange
        using var withoutSection = SandboxToolHarness.CreateRepository("""{ "version": 1, "deniedReadPaths": [] }""");
        using var withoutFile = SandboxToolHarness.CreateRepository(null);

        // Act
        var section = await Run(withoutSection.Path, "plan");
        var file = await Run(withoutFile.Path, "plan");

        // Assert
        section.ExitCode.ShouldBe(2);
        section.Error.ShouldContain("$.settings is required", StringComparison.Ordinal);
        file.ExitCode.ShouldBe(2);
        file.Error.ShouldContain("does not exist", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_root_that_does_not_exist_fails()
    {
        // Arrange
        using var repository = SandboxToolHarness.CreateRepository(null);

        // Act
        var (exitCode, _, error) = await Run(Path.Combine(repository.Path, "missing"), "plan");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("root directory does not exist", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_different_target_and_control_characters_in_output_are_escaped()
    {
        // Arrange
        using var repository = CreateRepository(Configuration("""{ "target": ".agent/local\u001b[31m.json", "allowWrite": ["~/x"] }"""));

        // Act
        var (exitCode, output, _) = await Run(repository.Path, "plan");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("settings: .agent/local\\u001B[31m.json", StringComparison.Ordinal);
        output.ShouldNotContain('\u001b'.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_local_settings_file_can_be_the_target()
    {
        // Arrange
        using var repository = CreateRepository(Configuration("""{ "target": ".claude/settings.local.json", "allowedDomains": ["a.example"] }"""));

        // Act
        var (exitCode, _, _) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(0);
        List(repository.Path, ".claude/settings.local.json", "sandbox", "network", "allowedDomains").ShouldBe(["a.example"]);
    }

    [Fact]
    public async Task A_non_boolean_allow_all_unix_sockets_is_warned_about_and_never_overwritten()
    {
        // Arrange
        const string Existing = """{ "sandbox": { "network": { "allowAllUnixSockets": "yes" } } }""";
        using var quiet = CreateRepository(Configuration("{}"), Existing);
        using var wanted = CreateRepository(Configuration("""{ "allowAllUnixSockets": true }"""), Existing);

        // Act
        var verify = await Run(quiet.Path, "verify");
        var apply = await Run(wanted.Path, "apply", "--force", "--accept-risk-allow-all-unix-sockets");

        // Assert
        verify.ExitCode.ShouldBe(0);
        verify.Output.ShouldContain("warning: $.sandbox.network.allowAllUnixSockets is not a boolean", StringComparison.Ordinal);
        apply.ExitCode.ShouldBe(2);
        apply.Error.ShouldContain("$.sandbox.network.allowAllUnixSockets must be a boolean", StringComparison.Ordinal);
        ReadText(Path.Combine(wanted.Path, DefaultTarget)).ShouldBe(Existing);
    }

    [Fact]
    public async Task A_lone_surrogate_in_an_unrelated_key_names_the_file_when_rewriting()
    {
        // Arrange
        using var repository = CreateRepository(Configuration(Dotnet), """{ "model": "\ud800" }""");

        // Act
        var (exitCode, _, error) = await Run(repository.Path, "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain(".claude/settings.json", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failure_removes_every_directory_that_the_run_created()
    {
        // Arrange
        using var repository = CreateRepository(Configuration("""{ "target": "a/b/c.json", "allowedDomains": ["x.example"] }"""));

        // Act
        var (exitCode, _, _) = await Run(repository.Path, () => throw new IOException("disk full"), "apply", "--force");

        // Assert
        exitCode.ShouldBe(2);
        Directory.Exists(Path.Combine(repository.Path, "a")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_wildcard_domain_below_a_registered_name_is_accepted()
    {
        // Arrange
        using var repository = CreateRepository(Configuration("""{ "allowedDomains": ["*.nuget.org", "*.example.co.uk:443"] }"""));

        // Act
        var (exitCode, _, _) = await Run(repository.Path, "plan");

        // Assert
        exitCode.ShouldBe(0);
    }
}
