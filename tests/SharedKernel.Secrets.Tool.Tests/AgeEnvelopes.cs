using System.Security.Cryptography;
using System.Text;

namespace SharedKernel.Secrets.Tool.Tests;

/// <summary>Builds an armored age file with one X25519 stanza that is unique to a recipient, like the <c>enc</c> values that sops writes.</summary>
internal static class AgeEnvelopes
{
    /// <summary>Returns the armored file for <paramref name="recipient"/>.</summary>
    public static string For(string recipient)
    {
        var share = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(recipient))).TrimEnd('=');
        var header = $"age-encryption.org/v1\n-> X25519 {share}\n{share}\n--- {share}\n";
        var body = Convert.ToBase64String(Encoding.ASCII.GetBytes(header).Concat(new byte[16]).ToArray());
        var lines = Enumerable.Range(0, (body.Length + 63) / 64).Select(i => body.Substring(i * 64, Math.Min(64, body.Length - (i * 64))));
        return "-----BEGIN AGE ENCRYPTED FILE-----\n" + string.Concat(lines.Select(static l => l + "\n")) + "-----END AGE ENCRYPTED FILE-----\n";
    }

    /// <summary>The armored file as a JSON string literal.</summary>
    public static string JsonLiteral(string recipient) => System.Text.Json.JsonSerializer.Serialize(For(recipient));

    /// <summary>The armored file, every line indented by <paramref name="spaces"/> and ended by a line feed.</summary>
    public static string Indented(string recipient, int spaces) =>
        string.Concat(For(recipient).TrimEnd('\n').Split('\n').Select(l => new string(' ', spaces) + l + "\n"));
}
