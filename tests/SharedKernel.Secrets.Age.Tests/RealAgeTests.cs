using System.Runtime.Versioning;
using System.Text;

namespace PandaBear.Secrets.Age.Tests;

/// <summary>
/// Runs the adapter against the real age and age-keygen programs with throwaway keys in temporary directories.
/// They skip when a program is not installed.
/// </summary>
[Trait("Capability", "Secrets")]
public sealed class RealAgeTests
{
    private const string AgeSkip = "age and age-keygen are not installed.";

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
}
