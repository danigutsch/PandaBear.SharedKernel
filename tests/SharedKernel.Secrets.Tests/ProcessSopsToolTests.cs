using System.Text;

namespace PandaBear.Secrets.Tests;

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
        request.Arguments.ShouldBe(["encrypt", "--age", $"{TestRecipients.Second},{TestRecipients.First}", "--input-type", "yaml", "--output-type", "yaml", "/dev/stdin"]);
        request.Arguments.ShouldNotContain(document);
        request.Environment.ShouldBeEmpty();
        Encoding.UTF8.GetString(runner.Inputs.ShouldHaveSingleItem()).ShouldBe(document);
    }

    [Fact]
    public async Task Encrypt_uses_the_json_types_for_json()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding("{}");
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var completed = await tool.Encrypt(SopsFormat.Json, TestRecipients.Set(TestRecipients.First), "{}"u8.ToArray(), TestContext.Current.CancellationToken);
        completed.ShouldBeSuccess();

        // Assert
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["encrypt", "--age", TestRecipients.First, "--input-type", "json", "--output-type", "json", "/dev/stdin"]);
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
        request.Arguments.ShouldBe(["decrypt", "/repo/secrets/app.sops.yaml"]);
        request.Environment.ShouldBe(new Dictionary<string, string> { ["SOPS_AGE_KEY_FILE"] = "/keys/id.txt" });
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
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["decrypt", "--extract", "[\"db\"][\"password\"]", Path.Combine(root.Path, "secrets", "app.sops.yaml")]);
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
    public async Task Rotate_runs_sops_rotate_in_place_with_the_identity_in_the_environment()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var completed = await tool.Rotate("/repo/app.sops.yaml", Identity, TestContext.Current.CancellationToken);
        completed.ShouldBeSuccess();

        // Assert
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.ShouldBe(["rotate", "-i", "/repo/app.sops.yaml"]);
        request.Environment["SOPS_AGE_KEY_FILE"].ShouldBe("/keys/id.txt");
    }

    [Fact]
    public async Task Rotate_reports_a_failure_and_refuses_a_relative_path()
    {
        // Arrange
        var failing = new ProcessSopsTool(FakeToolRunner.Failing(2, "no key could decrypt"), Sops);
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var failed = await failing.Rotate("/repo/app.sops.yaml", Identity, TestContext.Current.CancellationToken);
        var relative = await tool.Rotate("app.sops.yaml", Identity, TestContext.Current.CancellationToken);

        // Assert
        failed.IsFailure.ShouldBeTrue();
        relative.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateRecipients_adds_the_missing_recipient_and_checks_the_file_afterwards()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, SopsSamples.Yaml([TestRecipients.First]), TestContext.Current.CancellationToken);
        var runner = new FakeToolRunner(_ =>
        {
            File.WriteAllText(file, SopsSamples.Yaml([TestRecipients.First, TestRecipients.Second]));
            return FakeToolRunner.Result(0, string.Empty);
        });
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var beforeResult = await tool.UpdateRecipients(file, Rule, Identity, TestContext.Current.CancellationToken);
        var before = beforeResult.ShouldBeSuccess();

        // Assert
        before.Missing.Select(static r => r.Value).ShouldBe([TestRecipients.Second]);
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["rotate", "-i", "--add-age", TestRecipients.Second, file]);
    }

    [Fact]
    public async Task UpdateRecipients_removes_an_unexpected_recipient()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.json");
        await File.WriteAllTextAsync(file, SopsSamples.Json([TestRecipients.First, TestRecipients.Second, TestRecipients.Third]), TestContext.Current.CancellationToken);
        var runner = new FakeToolRunner(_ =>
        {
            File.WriteAllText(file, SopsSamples.Json([TestRecipients.First, TestRecipients.Second]));
            return FakeToolRunner.Result(0, string.Empty);
        });
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var completed = await tool.UpdateRecipients(file, Rule, Identity, TestContext.Current.CancellationToken);
        completed.ShouldBeSuccess();

        // Assert
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["rotate", "-i", "--rm-age", TestRecipients.Third, file]);
    }

    [Fact]
    public async Task UpdateRecipients_does_nothing_when_the_set_is_already_exact()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, SopsSamples.Yaml([TestRecipients.Second, TestRecipients.First]), TestContext.Current.CancellationToken);
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var verificationResult = await tool.UpdateRecipients(file, Rule, Identity, TestContext.Current.CancellationToken);
        var verification = verificationResult.ShouldBeSuccess();

        // Assert
        verification.IsExact.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateRecipients_refuses_a_file_with_non_age_key_holders_and_runs_nothing()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.json");
        await File.WriteAllTextAsync(file, SopsSamples.Json([TestRecipients.First], ",\"hc_vault\":[{\"vault_address\":\"https://v\"}]"), TestContext.Current.CancellationToken);
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var result = await tool.UpdateRecipients(file, Rule, Identity, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateRecipients_fails_when_sops_succeeds_but_the_file_is_still_not_the_rule_set()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, SopsSamples.Yaml([TestRecipients.First]), TestContext.Current.CancellationToken);
        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);

        // Act
        var result = await tool.UpdateRecipients(file, Rule, Identity, TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("still not the rule's recipient set", StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateRecipients_reports_a_failed_rotate()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, SopsSamples.Yaml([TestRecipients.First]), TestContext.Current.CancellationToken);
        var tool = new ProcessSopsTool(FakeToolRunner.Failing(128, "Failed to get the data key"), Sops);

        // Act
        var result = await tool.UpdateRecipients(file, Rule, Identity, TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("sops exited with code 128: Failed to get the data key");
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
            () => tool.Rotate(null!, Identity, token),
            () => tool.Rotate("/a", null!, token),
            () => tool.UpdateRecipients(null!, Rule, Identity, token),
            () => tool.UpdateRecipients("/a", null!, Identity, token),
            () => tool.UpdateRecipients("/a", Rule, null!, token),
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
