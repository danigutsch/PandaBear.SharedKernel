using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>
/// Assembles private key fixtures at runtime. Keys come from the BCL or from a double-base64 body file, and PEM
/// boundary lines are joined here, so no literal private key exists in the repository.
/// </summary>
internal static class PemFixtures
{
    /// <summary>Fingerprint printed by <c>ssh-keygen -lf</c> for the stored Ed25519 fixture.</summary>
    public const string Ed25519Fingerprint = "SHA256:flZ//nerex82Ax5aTqcKOjUi7Uw+a/e2kL7Z8xORd9w";

    /// <summary>Public key printed by <c>ssh-keygen</c> for the stored Ed25519 fixture, without its comment.</summary>
    public const string Ed25519PublicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIIF1Q4i5rO64OkVBzGOc+gh77/t6jfp4gcFMlztXf/Sx";

    private static readonly string Dashes = new('-', 5);

    public static string Boundary(string kind, string label)
    {
        return Dashes + kind + " " + label + Dashes;
    }

    public static string Ed25519OpenSsh()
    {
        string inner = Encoding.ASCII.GetString(Convert.FromBase64String(Fixture.Read("secrets/ed25519-openssh.body.b64")));
        return Wrap("OPENSSH PRIVATE KEY", inner, 70);
    }

    public static string Wrap(string label, string base64, int width = 64)
    {
        var builder = new StringBuilder();
        builder.Append(Boundary("BEGIN", label)).Append('\n');
        for (int offset = 0; offset < base64.Length; offset += width)
        {
            builder.Append(base64.AsSpan(offset, Math.Min(width, base64.Length - offset))).Append('\n');
        }

        builder.Append(Boundary("END", label));
        return builder.ToString();
    }

    public static string RsaPkcs1(RSA rsa)
    {
        return Wrap("RSA PRIVATE KEY", Convert.ToBase64String(rsa.ExportRSAPrivateKey()));
    }

    public static string Pkcs8(AsymmetricAlgorithm key)
    {
        return Wrap("PRIVATE KEY", Convert.ToBase64String(key.ExportPkcs8PrivateKey()));
    }

    public static string EncryptedPkcs8(AsymmetricAlgorithm key)
    {
        var parameters = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000);
        return Wrap("ENCRYPTED PRIVATE KEY", Convert.ToBase64String(key.ExportEncryptedPkcs8PrivateKey("fixture-pass"u8, parameters)));
    }

    /// <summary>OpenSSH container with a non-<c>none</c> cipher; the private section is random filler.</summary>
    public static string EncryptedOpenSsh()
    {
        using var stream = new MemoryStream();
        stream.Write("openssh-key-v1\0"u8);
        WriteString(stream, "aes256-ctr"u8);
        WriteString(stream, "bcrypt"u8);
        WriteString(stream, TestSecrets.Bytes(7, 24));
        WriteUInt32(stream, 1);
        using var publicBlob = new MemoryStream();
        WriteString(publicBlob, "ssh-ed25519"u8);
        WriteString(publicBlob, TestSecrets.Bytes(8, 32));
        WriteString(stream, publicBlob.ToArray());
        WriteString(stream, TestSecrets.Bytes(9, 160));
        return Wrap("OPENSSH PRIVATE KEY", Convert.ToBase64String(stream.ToArray()), 70);
    }

    private static void WriteString(MemoryStream stream, ReadOnlySpan<byte> value)
    {
        WriteUInt32(stream, (uint)value.Length);
        stream.Write(value);
    }

    private static void WriteUInt32(MemoryStream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }
}
