using System.Security.Cryptography;
using System.Text;

namespace PandaBear.Secrets.Tests;

/// <summary>Builds armored age files with chosen header stanzas, so tests can feed the metadata reader realistic enc values.</summary>
internal static class AgeEnvelopes
{
    /// <summary>An armored age file with the given stanza lines, such as <c>X25519 &lt;share&gt;</c>, each followed by a body line.</summary>
    public static string Armored(params string[] stanzas)
    {
        StringBuilder header = new("age-encryption.org/v1\n");
        foreach (var stanza in stanzas)
        {
            header.Append("-> ").Append(stanza).Append('\n').Append(Base64Unpadded(new byte[32])).Append('\n');
        }

        header.Append("--- ").Append(Base64Unpadded(new byte[32])).Append('\n');
        var bytes = Encoding.ASCII.GetBytes(header.ToString()).Concat(new byte[16]).ToArray();
        var body = Convert.ToBase64String(bytes);
        var lines = Enumerable.Range(0, (body.Length + 63) / 64).Select(i => body.Substring(i * 64, Math.Min(64, body.Length - (i * 64))));
        return "-----BEGIN AGE ENCRYPTED FILE-----\n" + string.Concat(lines.Select(static l => l + "\n")) + "-----END AGE ENCRYPTED FILE-----\n";
    }

    /// <summary>An armored file with the one stanza that age itself writes for <paramref name="recipient"/>.</summary>
    public static string For(string recipient)
    {
        var share = Base64Unpadded(SHA256.HashData(Encoding.UTF8.GetBytes(recipient)));
        if (!recipient.StartsWith("ssh-", StringComparison.Ordinal))
        {
            return Armored("X25519 " + share);
        }

        var parts = recipient.Split(' ');
        var tag = Base64Unpadded(SHA256.HashData(Convert.FromBase64String(parts[1]))[..4]);
        return Armored(parts[0] == "ssh-ed25519" ? $"ssh-ed25519 {tag} {share}" : $"ssh-rsa {tag}");
    }

    /// <summary>The armored file for <paramref name="recipient"/>, every line indented by <paramref name="spaces"/> spaces and ended by a line feed.</summary>
    public static string Indented(string recipient, int spaces) =>
        string.Concat(For(recipient).TrimEnd('\n').Split('\n').Select(l => new string(' ', spaces) + l + "\n"));

    /// <summary>The armored file for <paramref name="recipient"/> as a JSON string literal.</summary>
    public static string JsonLiteral(string recipient) => System.Text.Json.JsonSerializer.Serialize(For(recipient));

    /// <summary>Standard base64 without padding, as age writes it in headers.</summary>
    public static string Base64Unpadded(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');
}
