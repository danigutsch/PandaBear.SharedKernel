using System.Text;

namespace PandaBear.Secrets.Sops.Tests;

/// <summary>Tests the sops adapter with a fake process runner.</summary>
[Trait("Capability", "Secrets")]
public sealed class ProcessSopsToolTests
{
    private const string Sops = "/usr/local/bin/sops";

    private static readonly AgeIdentityRef Identity = AgeIdentityRef.Create("/keys/id.txt").ShouldBeSuccess();
    private static readonly SopsCreationRule Rule = TestRecipients.Rule("x", 2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public async Task Encrypt_passes_public_recipients_as_arguments_and_the_document_only_on_standard_input()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding("encrypted: document\n");
        var tool = new ProcessSopsTool(runner, Sops);
        const string document = "password: very-secret";

        // Act
        var encryptedResult = await tool.Encrypt(SopsFormat.Yaml, TestRecipients.Set(TestRecipients.First, TestRecipients.Second), Encoding.UTF8.GetBytes(document), TestContext.Current.CancellationToken);
        var encrypted = encryptedResult.ShouldBeSuccess();

        // Assert
        encrypted.ShouldBe("encrypted: document\n");
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Executable.ShouldBe(Sops);
        request.Arguments.ShouldBe(["--config", "/dev/null", "encrypt", "--age", $"{TestRecipients.Second},{TestRecipients.First}", "--input-type", "yaml", "--output-type", "yaml", "/dev/stdin"]);
        request.Arguments.ShouldNotContain(document);
        request.Environment.Keys.ToArray().ShouldBe(["HOME"]);
        Encoding.UTF8.GetString(runner.Inputs.ShouldHaveSingleItem()).ShouldBe(document);
    }

    [Fact]
    public async Task Encrypt_uses_the_JSON_types_for_JSON()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding("{}");
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var completed = await tool.Encrypt(SopsFormat.Json, TestRecipients.Set(TestRecipients.First), "{}"u8.ToArray(), TestContext.Current.CancellationToken);
        completed.ShouldBeSuccess();

        // Assert
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["--config", "/dev/null", "encrypt", "--age", TestRecipients.First, "--input-type", "json", "--output-type", "json", "/dev/stdin"]);
    }

    [Fact]
    public async Task A_failed_encrypt_is_an_error_result()
    {
        // Arrange
        var tool = new ProcessSopsTool(FakeToolRunner.Failing(1, "Error encrypting the data key"), Sops);

        // Act
        var result = await tool.Encrypt(SopsFormat.Yaml, TestRecipients.Set(TestRecipients.First), default, TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("sops exited with code 1: Error encrypting the data key");
    }

    [Fact]
    public async Task Every_sops_call_ignores_any_config_file_and_runs_in_an_empty_directory_that_is_removed_afterwards()
    {
        // Arrange
        List<(string Directory, bool Empty)> seen = [];
        var runner = new FakeToolRunner(request =>
        {
            seen.Add((request.WorkingDirectory ?? string.Empty, request.WorkingDirectory is not null && Directory.Exists(request.WorkingDirectory) && !Directory.EnumerateFileSystemEntries(request.WorkingDirectory).Any()));
            return FakeToolRunner.Result(0, "x");
        });
        var tool = new ProcessSopsTool(runner, Sops);
        var reference = SecretRef.Create("a.sops.yaml#k").ShouldBeSuccess();

        // Act
        await tool.Encrypt(SopsFormat.Yaml, TestRecipients.Set(TestRecipients.First), "a: b"u8.ToArray(), TestContext.Current.CancellationToken);
        await tool.Decrypt("/repo/a.sops.yaml", Identity, TestContext.Current.CancellationToken);
        await tool.DecryptValue("/repo", reference, Identity, TestContext.Current.CancellationToken);

        // Assert
        runner.Requests.Count.ShouldBe(3);
        foreach (var request in runner.Requests)
        {
            request.Arguments.Take(2).ToArray().ShouldBe(["--config", "/dev/null"]);
        }

        foreach (var (directory, empty) in seen)
        {
            empty.ShouldBeTrue();
            Directory.Exists(directory).ShouldBeFalse();
        }

        seen.Select(static entry => entry.Directory).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task Sops_gets_an_empty_home_so_that_keys_in_the_callers_home_directory_cannot_decrypt()
    {
        // Arrange
        List<(string? Home, string? WorkingDirectory)> seen = [];
        var runner = new FakeToolRunner(request =>
        {
            seen.Add((request.Environment.GetValueOrDefault("HOME"), request.WorkingDirectory));
            return FakeToolRunner.Result(0, "x");
        });
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        await tool.Decrypt("/repo/a.sops.yaml", Identity, TestContext.Current.CancellationToken);
        await tool.TrialDecrypt("/repo/a.sops.yaml", Identity, TestContext.Current.CancellationToken);
        await tool.Encrypt(SopsFormat.Yaml, TestRecipients.Set(TestRecipients.First), "a: b"u8.ToArray(), TestContext.Current.CancellationToken);

        // Assert
        seen.Count.ShouldBe(3);
        foreach (var (home, directory) in seen)
        {
            home.ShouldNotBeNull();
            home.ShouldBe(directory);
        }
    }

    [Fact]
    public async Task A_trial_that_decrypts_is_decrypted_and_the_plaintext_is_not_returned()
    {
        // Arrange
        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding("SECRET-PLAINTEXT"), Sops);

        // Act
        var trial = await tool.TrialDecrypt("/repo/a.sops.yaml", Identity, TestContext.Current.CancellationToken);

        // Assert
        trial.Outcome.ShouldBe(SopsTrialOutcome.Decrypted);
        trial.Detail.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Only_an_identity_that_matches_no_recipient_is_a_verdict()
    {
        // Arrange
        const string noMatch = "Failed to get the data key required to decrypt the SOPS file.\n\nGroup 0: FAILED\n  age1x: FAILED\n    - | failed to create reader for decrypting sops data key with\n      | age: identity did not match any of the recipients: incorrect\n      | identity for recipient block.\n";
        var tool = new ProcessSopsTool(FakeToolRunner.Failing(128, noMatch), Sops);

        // Act
        var trial = await tool.TrialDecrypt("/repo/a.sops.yaml", Identity, TestContext.Current.CancellationToken);

        // Assert
        trial.Outcome.ShouldBe(SopsTrialOutcome.NoMatchingIdentity);
    }

    [Theory]
    [InlineData("Failed to get the data key required to decrypt the SOPS file.\n\nGroup 0: FAILED\n    - | failed to load age identities. Errors while loading age\n      | identities: unknown identity type.\n")]
    [InlineData("Failed to get the data key required to decrypt the SOPS file.\n\nGroup 0: FAILED\n    - | failed to create reader for decrypting sops data key with\n      | age: failed to read header: parsing age header: unexpected intro\n")]
    [InlineData("Failed to get the data key required to decrypt the SOPS file.\n    - | age: identity did not match any of the recipients\n    - | failed to load age identities.\n")]
    [InlineData("Cannot decrypt MAC: Error base64-decoding data: illegal base64 data at input byte 172\n")]
    [InlineData("Error: cannot operate on non-existent file \"/x\"\n")]
    [InlineData("yaml: line 1: did not find expected node content\n")]
    [InlineData("")]
    public async Task Every_other_failure_is_no_verdict_about_the_identity(string standardError)
    {
        // Arrange
        var tool = new ProcessSopsTool(FakeToolRunner.Failing(128, standardError), Sops);

        // Act
        var trial = await tool.TrialDecrypt("/repo/a.sops.yaml", Identity, TestContext.Current.CancellationToken);

        // Assert
        trial.Outcome.ShouldBe(SopsTrialOutcome.Failed);
        trial.Detail.ShouldStartWith("sops exited with code 128", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_process_that_cannot_run_or_a_relative_file_is_no_verdict_either()
    {
        // Arrange
        var timedOut = new ProcessSopsTool(FakeToolRunner.Throwing(new TimeoutException("The process did not finish within its time limit.")), Sops);
        var unused = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);

        // Act
        var timeout = await timedOut.TrialDecrypt("/repo/a.sops.yaml", Identity, TestContext.Current.CancellationToken);
        var relative = await unused.TrialDecrypt("a.sops.yaml", Identity, TestContext.Current.CancellationToken);

        // Assert
        timeout.Outcome.ShouldBe(SopsTrialOutcome.Failed);
        timeout.Detail.ShouldContain("could not run", StringComparison.Ordinal);
        relative.Outcome.ShouldBe(SopsTrialOutcome.Failed);
    }

    [Fact]
    public async Task A_trial_rejects_null()
    {
        // Arrange
        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);

        // Act
        Func<Task> noFile = () => tool.TrialDecrypt(null!, Identity, TestContext.Current.CancellationToken);
        Func<Task> noIdentity = () => tool.TrialDecrypt("/a", null!, TestContext.Current.CancellationToken);

        // Assert
        await noFile.ShouldThrow<ArgumentNullException>();
        await noIdentity.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public async Task Reading_recipients_from_a_file_that_never_finishes_is_an_error_not_a_hang()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var fifo = Path.Combine(directory.Path, "pipe.sops.yaml");
        using var made = await new SystemToolRunner().Run(new ToolRequest("/usr/bin/mkfifo", [fifo]), TestContext.Current.CancellationToken);

        // Act
        var result = await SopsFileReader.ReadRecipients(fifo, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(300));

        // Assert
        made.ExitCode.ShouldBe(0);
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("did not finish", StringComparison.Ordinal);
    }


    [Fact]
    public async Task Decrypt_gives_the_child_only_the_identity_file_path_in_its_environment()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding("db:\n  password: hunter2\n");
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var plaintextResult = await tool.Decrypt("/repo/secrets/app.sops.yaml", Identity, TestContext.Current.CancellationToken);
        using var plaintext = plaintextResult.ShouldBeSuccess();

        // Assert
        plaintext.RevealUtf8().ShouldContain("hunter2", StringComparison.Ordinal);
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.ShouldBe(["--config", "/dev/null", "decrypt", "/repo/secrets/app.sops.yaml"]);
        request.Environment.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["HOME", "SOPS_AGE_KEY_FILE"]);
        request.Environment["SOPS_AGE_KEY_FILE"].ShouldBe("/keys/id.txt");
        runner.Inputs.ShouldHaveSingleItem().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("relative/app.sops.yaml")]
    [InlineData("")]
    [InlineData("/repo/app\n.sops.yaml")]
    public async Task Decrypt_refuses_a_file_path_that_is_not_a_safe_absolute_path(string file)
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var result = await tool.Decrypt(file, Identity, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecryptValue_extracts_one_value_by_its_key_path()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        var runner = FakeToolRunner.Succeeding("hunter2");
        var tool = new ProcessSopsTool(runner, Sops);
        var reference = SecretRef.Create("secrets/app.sops.yaml#db.password").ShouldBeSuccess();

        // Act
        var valueResult = await tool.DecryptValue(root.Path, reference, Identity, TestContext.Current.CancellationToken);
        using var value = valueResult.ShouldBeSuccess();

        // Assert
        value.RevealUtf8().ShouldBe("hunter2");
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["--config", "/dev/null", "decrypt", "--extract", "[\"db\"][\"password\"]", Path.Combine(root.Path, "secrets", "app.sops.yaml")]);
    }

    [Fact]
    public async Task DecryptValue_refuses_a_relative_root()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var result = await tool.DecryptValue("relative", SecretRef.Create("a.sops.yaml#k").ShouldBeSuccess(), Identity, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task VerifyRecipients_compares_a_file_with_a_rule_without_starting_a_process()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var good = Path.Combine(directory.Path, "good.sops.yaml");
        var bad = Path.Combine(directory.Path, "bad.sops.json");
        await File.WriteAllTextAsync(good, SopsSamples.Yaml([TestRecipients.First, TestRecipients.Second]), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(bad, SopsSamples.Json([TestRecipients.First]), TestContext.Current.CancellationToken);
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var exactResult = await tool.VerifyRecipients(good, Rule, TestContext.Current.CancellationToken);
        var exact = exactResult.ShouldBeSuccess();
        var subsetResult = await tool.VerifyRecipients(bad, Rule, TestContext.Current.CancellationToken);
        var subset = subsetResult.ShouldBeSuccess();

        // Assert
        exact.IsExact.ShouldBeTrue();
        subset.IsExact.ShouldBeFalse();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListRecipients_reports_a_missing_file_an_unsupported_format_an_empty_file_and_a_relative_path()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var empty = Path.Combine(directory.Path, "empty.sops.yaml");
        await File.WriteAllTextAsync(empty, string.Empty, TestContext.Current.CancellationToken);
        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);

        // Act
        var missing = await tool.ListRecipients(Path.Combine(directory.Path, "missing.sops.yaml"), TestContext.Current.CancellationToken);
        var unsupported = await tool.ListRecipients(Path.Combine(directory.Path, "a.sops.env"), TestContext.Current.CancellationToken);
        var emptyResult = await tool.ListRecipients(empty, TestContext.Current.CancellationToken);
        var relative = await tool.ListRecipients("a.sops.yaml", TestContext.Current.CancellationToken);

        // Assert
        missing.IsFailure.ShouldBeTrue();
        unsupported.IsFailure.ShouldBeTrue();
        emptyResult.IsFailure.ShouldBeTrue();
        relative.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ListRecipients_refuses_a_file_larger_than_the_limit()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var big = Path.Combine(directory.Path, "big.sops.json");
        await using (var stream = File.Create(big))
        {
            stream.SetLength((16 * 1024 * 1024) + 1);
        }

        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);

        // Act
        var result = await tool.ListRecipients(big, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ListRecipients_reports_a_directory_named_like_a_file_as_an_error()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var folder = Path.Combine(directory.Path, "dir.sops.yaml");
        Directory.CreateDirectory(folder);
        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);

        // Act
        var result = await tool.ListRecipients(folder, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void The_adapter_requires_an_absolute_tool_path_and_non_null_arguments()
    {
        // Act
        Action relative = () => _ = new ProcessSopsTool(FakeToolRunner.Succeeding(), "sops");
        Action noRunner = () => _ = new ProcessSopsTool(null!, Sops);
        Action noPath = () => _ = new ProcessSopsTool(FakeToolRunner.Succeeding(), null!);

        // Assert
        relative.ShouldThrow<ArgumentException>();
        noRunner.ShouldThrow<ArgumentNullException>();
        noPath.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public async Task Null_arguments_are_refused()
    {
        // Arrange
        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);
        var token = TestContext.Current.CancellationToken;
        var reference = SecretRef.Create("a.sops.yaml#k").ShouldBeSuccess();

        // Act
        var calls = new Func<Task>[]
        {
            () => tool.Encrypt(SopsFormat.Yaml, null!, default, token),
            () => tool.Decrypt(null!, Identity, token),
            () => tool.Decrypt("/a", null!, token),
            () => tool.DecryptValue(null!, reference, Identity, token),
            () => tool.DecryptValue("/a", null!, Identity, token),
            () => tool.DecryptValue("/a", reference, null!, token),
            () => tool.ListRecipients(null!, token),
            () => tool.VerifyRecipients("/a", null!, token),
        };

        // Assert
        foreach (var call in calls)
        {
            await call.ShouldThrow<ArgumentNullException>();
        }
    }
}
