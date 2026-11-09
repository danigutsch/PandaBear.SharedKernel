namespace PandaBear.Secrets.Tests;

/// <summary>Public recipients of throwaway keys, for tests. No private key is kept anywhere.</summary>
internal static class TestRecipients
{
    /// <summary>A valid X25519 recipient.</summary>
    public const string First = "age1s6ljywnmxkxndew9qztcjgfa3lsqfwe0rec66a9jjugzuxaagg3sgumkyf";

    /// <summary>A second valid X25519 recipient.</summary>
    public const string Second = "age1pz4srxz4yn6h4puyx9mgzk63pn4tuhyla67rrglluyqjnlhyz5sqx2vc4h";

    /// <summary>A third valid X25519 recipient.</summary>
    public const string Third = "age1dquc283cn3pehdzu8pyk68ucd4eq5x6d9ru5qj67zdd7pq7gkdeq5raw4x";

    /// <summary>A syntactically valid ssh-ed25519 recipient line with a made-up key blob (the bytes 1 to 32), not a real key.</summary>
    public const string SshEd25519 = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8g";

    /// <summary>A syntactically valid ssh-rsa recipient line with a made-up key blob.</summary>
    public const string SshRsa = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAAABACAAAE=";

    /// <summary>Creates a recipient or fails the test.</summary>
    public static AgeRecipient Recipient(string text) => AgeRecipient.Create(text).ShouldBeSuccess();

    /// <summary>Creates a set or fails the test.</summary>
    public static AgeRecipientSet Set(params string[] texts) => AgeRecipientSet.Create(texts.Select(Recipient)).ShouldBeSuccess();

    /// <summary>Creates a rule with the given pattern and recipients, requiring at least <paramref name="minimum"/>.</summary>
    public static SopsCreationRule Rule(string pattern, int minimum, params string[] texts) =>
        SopsCreationRule.Create(pattern, Set(texts), minimum).ShouldBeSuccess();

    /// <summary>Creates an encrypted-file path or fails the test.</summary>
    public static EncryptedFilePath File(string path) => EncryptedFilePath.Create(path).ShouldBeSuccess();
}
