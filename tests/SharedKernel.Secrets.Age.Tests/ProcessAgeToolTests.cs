using System.Runtime.Versioning;
using System.Text;

namespace SharedKernel.Secrets.Age.Tests;

/// <summary>Tests the age adapter with a fake process runner.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class ProcessAgeToolTests
{
    private const string Age = "/usr/bin/age";
    private const string Keygen = "/usr/bin/age-keygen";

    [Fact]
    public async Task Encrypt_sends_recipients_as_arguments_and_the_plaintext_only_on_standard_input()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding("-----BEGIN AGE ENCRYPTED FILE-----\nAAAA\n-----END AGE ENCRYPTED FILE-----\n");
        var tool = new ProcessAgeTool(runner, Age, Keygen);
        const string plaintext = "very-secret-plaintext";

        // Act
        var armoredResult = await tool.Encrypt(TestRecipients.Set(TestRecipients.First, TestRecipients.Second), Encoding.UTF8.GetBytes(plaintext), TestContext.Current.CancellationToken);
        var armored = armoredResult.ShouldBeSuccess();

        // Assert
        armored.ShouldStartWith("-----BEGIN AGE ENCRYPTED FILE-----", StringComparison.Ordinal);
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Executable.ShouldBe(Age);
        request.Arguments.ShouldBe(["--encrypt", "--armor", "-r", TestRecipients.Second, "-r", TestRecipients.First]);
        request.Arguments.ShouldNotContain(plaintext);
        request.Environment.ShouldBeEmpty();
        Encoding.UTF8.GetString(runner.Inputs.ShouldHaveSingleItem()).ShouldBe(plaintext);
    }

    [Fact]
    public async Task Decrypt_passes_the_identity_file_path_and_the_ciphertext_on_standard_input()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding("the plaintext");
        var tool = new ProcessAgeTool(runner, Age, Keygen);
        var identity = AgeIdentityRef.Create("/keys/id.txt").ShouldBeSuccess();

        // Act
        var plaintextResult = await tool.Decrypt(identity, "ciphertext"u8.ToArray(), TestContext.Current.CancellationToken);
        using var plaintext = plaintextResult.ShouldBeSuccess();

        // Assert
        plaintext.RevealUtf8().ShouldBe("the plaintext");
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.ShouldBe(["--decrypt", "-i", "/keys/id.txt"]);
        request.Environment.ShouldBeEmpty();
        runner.Inputs.ShouldHaveSingleItem().ShouldBe("ciphertext"u8.ToArray());
    }

    [Fact]
    public async Task A_failed_decrypt_reports_the_first_error_line_and_no_output()
    {
        // Arrange
        var runner = FakeToolRunner.Failing(1, "age: error: no identity matched any of the recipients\nage: report at https://example\n");
        var tool = new ProcessAgeTool(runner, Age, Keygen);

        // Act
        var result = await tool.Decrypt(AgeIdentityRef.Create("/keys/id.txt").ShouldBeSuccess(), "x"u8.ToArray(), TestContext.Current.CancellationToken);

        // Assert
        var detail = result.ErrorDetails.ShouldNotBeNull().Detail;
        detail.ShouldBe("age exited with code 1: age: error: no identity matched any of the recipients");
    }

    [Fact]
    public async Task An_error_line_is_cleaned_of_control_characters_and_capped()
    {
        // Arrange
        var runner = FakeToolRunner.Failing(1, "bad\u001b[31m" + new string('x', 500));
        var tool = new ProcessAgeTool(runner, Age, Keygen);

        // Act
        var result = await tool.Encrypt(TestRecipients.Set(TestRecipients.First), default, TestContext.Current.CancellationToken);

        // Assert
        var detail = result.ErrorDetails.ShouldNotBeNull().Detail;
        detail.ShouldNotContain("\u001b", StringComparison.Ordinal);
        detail.Length.ShouldBeLessThan(260);
    }

    [Fact]
    public async Task A_process_that_cannot_run_is_an_error_result_not_an_exception()
    {
        // Arrange
        var tool = new ProcessAgeTool(FakeToolRunner.Throwing(new TimeoutException("The process did not finish within its time limit.")), Age, Keygen);

        // Act
        var result = await tool.Encrypt(TestRecipients.Set(TestRecipients.First), default, TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("age could not run: The process did not finish within its time limit.");
    }

    [Fact]
    public async Task GenerateIdentity_runs_age_keygen_with_an_output_path_and_returns_only_the_public_recipient()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "id.txt");
        var runner = FakeToolRunner.Succeeding(string.Empty, $"Public key: {TestRecipients.First}\n");
        var tool = new ProcessAgeTool(runner, Age, Keygen);

        // Act
        var recipientResult = await tool.GenerateIdentity(AgeIdentityRef.Create(path).ShouldBeSuccess(), TestContext.Current.CancellationToken);
        var recipient = recipientResult.ShouldBeSuccess();

        // Assert
        recipient.Value.ShouldBe(TestRecipients.First);
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Executable.ShouldBe(Keygen);
        request.Arguments.ShouldBe(["-o", path]);
        request.Environment.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateIdentity_reads_the_public_key_from_standard_output_too()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var tool = new ProcessAgeTool(FakeToolRunner.Succeeding($"# comment\nPublic key: {TestRecipients.Second}\n"), Age, Keygen);

        // Act
        var recipientResult = await tool.GenerateIdentity(AgeIdentityRef.Create(Path.Combine(directory.Path, "id.txt")).ShouldBeSuccess(), TestContext.Current.CancellationToken);
        var recipient = recipientResult.ShouldBeSuccess();

        // Assert
        recipient.Value.ShouldBe(TestRecipients.Second);
    }

    [Fact]
    public async Task GenerateIdentity_never_overwrites_an_existing_file_and_runs_nothing()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "id.txt");
        await File.WriteAllTextAsync(path, "existing", TestContext.Current.CancellationToken);
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessAgeTool(runner, Age, Keygen);

        // Act
        var result = await tool.GenerateIdentity(AgeIdentityRef.Create(path).ShouldBeSuccess(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
        var content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        content.ShouldBe("existing");
    }

    [Fact]
    public async Task GenerateIdentity_needs_an_existing_directory()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessAgeTool(runner, Age, Keygen);

        // Act
        var result = await tool.GenerateIdentity(AgeIdentityRef.Create("/nonexistent-directory-for-test/id.txt").ShouldBeSuccess(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("no key here\n")]
    [InlineData("Public key: not-a-recipient\n")]
    public async Task GenerateIdentity_fails_when_no_valid_public_key_is_printed(string output)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var tool = new ProcessAgeTool(FakeToolRunner.Succeeding(string.Empty, output), Age, Keygen);

        // Act
        var result = await tool.GenerateIdentity(AgeIdentityRef.Create(Path.Combine(directory.Path, "id.txt")).ShouldBeSuccess(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task GenerateIdentity_fails_when_the_file_is_left_readable_by_others()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "id.txt");
        var runner = new FakeToolRunner(_ =>
        {
            File.WriteAllText(path, "key");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
            return FakeToolRunner.Result(0, string.Empty, $"Public key: {TestRecipients.First}\n");
        });
        var tool = new ProcessAgeTool(runner, Age, Keygen);

        // Act
        var result = await tool.GenerateIdentity(AgeIdentityRef.Create(path).ShouldBeSuccess(), TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("readable by others", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_keygen_is_an_error_result()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var tool = new ProcessAgeTool(FakeToolRunner.Failing(1, "age-keygen: error: failed"), Age, Keygen);

        // Act
        var result = await tool.GenerateIdentity(AgeIdentityRef.Create(Path.Combine(directory.Path, "id.txt")).ShouldBeSuccess(), TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("age-keygen exited with code 1: age-keygen: error: failed");
    }

    [Fact]
    public void The_adapter_requires_absolute_tool_paths_and_arguments()
    {
        // Act
        Action relative = () => _ = new ProcessAgeTool(FakeToolRunner.Succeeding(), "age", Keygen);
        Action relativeKeygen = () => _ = new ProcessAgeTool(FakeToolRunner.Succeeding(), Age, "age-keygen");
        Action noRunner = () => _ = new ProcessAgeTool(null!, Age, Keygen);
        Action noAge = () => _ = new ProcessAgeTool(FakeToolRunner.Succeeding(), null!, Keygen);
        Action noKeygen = () => _ = new ProcessAgeTool(FakeToolRunner.Succeeding(), Age, null!);

        // Assert
        relative.ShouldThrow<ArgumentException>();
        relativeKeygen.ShouldThrow<ArgumentException>();
        noRunner.ShouldThrow<ArgumentNullException>();
        noAge.ShouldThrow<ArgumentNullException>();
        noKeygen.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public async Task Null_arguments_are_refused()
    {
        // Arrange
        var tool = new ProcessAgeTool(FakeToolRunner.Succeeding(), Age, Keygen);

        // Act
        Func<Task> noDestination = () => tool.GenerateIdentity(null!, TestContext.Current.CancellationToken);
        Func<Task> noRecipients = () => tool.Encrypt(null!, default, TestContext.Current.CancellationToken);
        Func<Task> noIdentity = () => tool.Decrypt(null!, default, TestContext.Current.CancellationToken);

        // Assert
        await noDestination.ShouldThrow<ArgumentNullException>();
        await noRecipients.ShouldThrow<ArgumentNullException>();
        await noIdentity.ShouldThrow<ArgumentNullException>();
    }
}
