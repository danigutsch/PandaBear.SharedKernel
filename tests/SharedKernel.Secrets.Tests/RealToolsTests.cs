using System.Runtime.Versioning;
using System.Text;

namespace PandaBear.Secrets.Tests;

/// <summary>
/// Runs the adapters against the real age, age-keygen, and sops programs with throwaway keys in temporary directories.
/// They skip when a program is not installed.
/// </summary>
[Trait("Capability", "Secrets")]
public sealed class RealToolsTests
{
    private const string AgeSkip = "age and age-keygen are not installed.";
    private const string AllSkip = "age, age-keygen, and sops are not all installed.";

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AgeAvailable), Skip = AgeSkip)]
    [UnsupportedOSPlatform("windows")]
    public async Task Keygen_writes_an_owner_only_identity_file_and_returns_only_the_recipient()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var (identity, recipient) = await RealTools.NewKey(directory.Path, "id.txt", TestContext.Current.CancellationToken);

        // Assert
        recipient.Kind.ShouldBe(AgeRecipientKind.X25519);
        File.GetUnixFileMode(identity.Path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var content = await File.ReadAllTextAsync(identity.Path, TestContext.Current.CancellationToken);
        content.ShouldContain("AGE-SECRET-KEY-", StringComparison.Ordinal);
        recipient.Value.ShouldNotContain("SECRET", StringComparison.OrdinalIgnoreCase);
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AgeAvailable), Skip = AgeSkip)]
    public async Task A_second_keygen_to_the_same_path_is_refused_and_the_key_is_unchanged()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var (identity, _) = await RealTools.NewKey(directory.Path, "id.txt", TestContext.Current.CancellationToken);
        var before = await File.ReadAllTextAsync(identity.Path, TestContext.Current.CancellationToken);

        // Act
        var again = await RealTools.Age().GenerateIdentity(identity, TestContext.Current.CancellationToken);

        // Assert
        again.IsFailure.ShouldBeTrue();
        var after = await File.ReadAllTextAsync(identity.Path, TestContext.Current.CancellationToken);
        after.ShouldBe(before);
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AgeAvailable), Skip = AgeSkip)]
    public async Task Age_encrypts_to_two_recipients_and_either_identity_decrypts()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (identityA, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (identityB, recipientB) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var tool = RealTools.Age();

        // Act
        var armoredResult = await tool.Encrypt(AgeRecipientSet.Create([recipientA, recipientB]).ShouldBeSuccess(), Encoding.UTF8.GetBytes("plain text"), token);
        var armored = armoredResult.ShouldBeSuccess();
        var viaAResult = await tool.Decrypt(identityA, Encoding.UTF8.GetBytes(armored), token);
        using var viaA = viaAResult.ShouldBeSuccess();
        var viaBResult = await tool.Decrypt(identityB, Encoding.UTF8.GetBytes(armored), token);
        using var viaB = viaBResult.ShouldBeSuccess();

        // Assert
        armored.ShouldStartWith("-----BEGIN AGE ENCRYPTED FILE-----", StringComparison.Ordinal);
        armored.ShouldNotContain("plain text", StringComparison.Ordinal);
        viaA.RevealUtf8().ShouldBe("plain text");
        viaB.RevealUtf8().ShouldBe("plain text");
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AgeAvailable), Skip = AgeSkip)]
    public async Task Age_decryption_with_an_identity_that_is_not_a_recipient_fails_with_a_short_message()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (_, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (identityB, _) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var tool = RealTools.Age();
        var armoredResult = await tool.Encrypt(AgeRecipientSet.Create([recipientA]).ShouldBeSuccess(), "x"u8.ToArray(), token);
        var armored = armoredResult.ShouldBeSuccess();

        // Act
        var result = await tool.Decrypt(identityB, Encoding.UTF8.GetBytes(armored), token);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldStartWith("age exited with code", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task Sops_round_trip_with_real_tools_in_yaml_and_json()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (identityA, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (_, recipientB) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var recipients = AgeRecipientSet.Create([recipientA, recipientB]).ShouldBeSuccess();
        var sops = RealTools.Sops();
        var rule = SopsCreationRule.Create(@"\.sops\.", recipients, 2).ShouldBeSuccess();

        // Act
        var yamlResult = await sops.Encrypt(SopsFormat.Yaml, recipients, "db:\n  password: hunter2\nlist:\n  - one\n"u8.ToArray(), token);
        var yaml = yamlResult.ShouldBeSuccess();
        var jsonResult = await sops.Encrypt(SopsFormat.Json, recipients, "{\"db\":{\"password\":\"hunter2\"}}"u8.ToArray(), token);
        var json = jsonResult.ShouldBeSuccess();
        var yamlFile = Path.Combine(directory.Path, "app.sops.yaml");
        var jsonFile = Path.Combine(directory.Path, "app.sops.json");
        await File.WriteAllTextAsync(yamlFile, yaml, token);
        await File.WriteAllTextAsync(jsonFile, json, token);
        var wholeResult = await sops.Decrypt(yamlFile, identityA, token);
        using var whole = wholeResult.ShouldBeSuccess();
        var reference = SecretRef.Create("app.sops.yaml#db.password").ShouldBeSuccess();
        var valueResult = await sops.DecryptValue(directory.Path, reference, identityA, token);
        using var value = valueResult.ShouldBeSuccess();
        var jsonValueResult = await sops.DecryptValue(directory.Path, SecretRef.Create("app.sops.json#db.password").ShouldBeSuccess(), identityA, token);
        using var jsonValue = jsonValueResult.ShouldBeSuccess();
        var yamlVerificationResult = await sops.VerifyRecipients(yamlFile, rule, token);
        var yamlVerification = yamlVerificationResult.ShouldBeSuccess();
        var jsonVerificationResult = await sops.VerifyRecipients(jsonFile, rule, token);
        var jsonVerification = jsonVerificationResult.ShouldBeSuccess();

        // Assert
        yaml.ShouldNotContain("hunter2", StringComparison.Ordinal);
        whole.RevealUtf8().ShouldContain("hunter2", StringComparison.Ordinal);
        value.RevealUtf8().ShouldBe("hunter2");
        jsonValue.RevealUtf8().ShouldBe("hunter2");
        yamlVerification.IsExact.ShouldBeTrue();
        jsonVerification.IsExact.ShouldBeTrue();
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task Update_recipients_with_real_sops_adds_and_removes_recipients_and_the_data_survives()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (identityA, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (_, recipientB) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var (identityC, recipientC) = await RealTools.NewKey(directory.Path, "c.txt", token);
        var sops = RealTools.Sops();
        var original = AgeRecipientSet.Create([recipientA, recipientB]).ShouldBeSuccess();
        var target = AgeRecipientSet.Create([recipientA, recipientC]).ShouldBeSuccess();
        var rule = SopsCreationRule.Create(@"\.sops\.", target, 2).ShouldBeSuccess();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await RealTools.EncryptToFile(sops, file, original, "token: abc123\n", token);

        // Act
        var beforeResult = await sops.UpdateRecipients(file, rule, identityA, token);
        var before = beforeResult.ShouldBeSuccess();
        var afterResult = await sops.VerifyRecipients(file, rule, token);
        var after = afterResult.ShouldBeSuccess();
        var viaCResult = await sops.Decrypt(file, identityC, token);
        using var viaC = viaCResult.ShouldBeSuccess();
        var completed = await sops.Rotate(file, identityA, token);
        completed.ShouldBeSuccess();
        var afterRotateResult = await sops.VerifyRecipients(file, rule, token);
        var afterRotate = afterRotateResult.ShouldBeSuccess();

        // Assert
        before.Missing.Select(static r => r.Value).ShouldBe([recipientC.Value]);
        before.Unexpected.Select(static r => r.Value).ShouldBe([recipientB.Value]);
        after.IsExact.ShouldBeTrue();
        afterRotate.IsExact.ShouldBeTrue();
        viaC.RevealUtf8().ShouldContain("abc123", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(RealTools), SkipUnless = nameof(RealTools.AllAvailable), Skip = AllSkip)]
    public async Task Sops_decryption_with_the_wrong_identity_fails_without_plaintext_in_the_message()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var token = TestContext.Current.CancellationToken;
        var (_, recipientA) = await RealTools.NewKey(directory.Path, "a.txt", token);
        var (identityB, _) = await RealTools.NewKey(directory.Path, "b.txt", token);
        var sops = RealTools.Sops();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await RealTools.EncryptToFile(sops, file, AgeRecipientSet.Create([recipientA]).ShouldBeSuccess(), "token: needle-value\n", token);

        // Act
        var result = await sops.Decrypt(file, identityB, token);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldNotContain("needle-value", StringComparison.Ordinal);
    }
}
