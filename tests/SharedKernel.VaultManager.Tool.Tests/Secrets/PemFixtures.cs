using System.Buffers.Binary;
using System.Formats.Asn1;
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

    public static string Sec1(ECDsa key)
    {
        return Wrap("EC PRIVATE KEY", Convert.ToBase64String(key.ExportECPrivateKey()));
    }

    /// <summary>A new unencrypted key: PKCS#1 RSA for <c>RSA PRIVATE KEY</c>, otherwise SEC1 P-256.</summary>
    public static string Unencrypted(string label)
    {
        if (label == "RSA PRIVATE KEY")
        {
            using var rsa = RSA.Create(2048);
            return RsaPkcs1(rsa);
        }

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return Sec1(ecdsa);
    }

    /// <summary>
    /// Breaks the base64 body of a PEM block and keeps its boundary lines: the first body line either starts with a
    /// character outside the base64 alphabet or loses three characters.
    /// </summary>
    public static string WithBrokenBody(string pem, bool shortenLine)
    {
        string[] lines = pem.Split('\n');
        lines[1] = shortenLine ? lines[1][3..] : "*" + lines[1][1..];
        return string.Join('\n', lines);
    }

    /// <summary>
    /// SEC1 P-256 key whose last base64 character before the padding has its unused bits set. The body still decodes
    /// to the same key, but <see cref="PemEncoding"/> rejects it as non-canonical base64.
    /// </summary>
    public static string Sec1WithNonzeroPaddingBits(ECDsa key)
    {
        string base64 = Convert.ToBase64String(key.ExportECPrivateKey());
        int index = base64.TrimEnd('=').Length - 1;
        int unusedBits = base64.EndsWith("==", StringComparison.Ordinal) ? 0x0F : 0x03;
        char replacement = TestSecrets.Base64[TestSecrets.Base64.IndexOf(base64[index], StringComparison.Ordinal) | unusedBits];
        return Wrap("EC PRIVATE KEY", base64[..index] + replacement + base64[(index + 1)..]);
    }

    /// <summary>SEC1 key whose named curve is <c>2.999.1</c>, under the OID arc reserved for examples, which no platform implements.</summary>
    public static string Sec1OnUnknownCurve()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            writer.WriteOctetString(TestSecrets.Bytes(41, 32));
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
            {
                writer.WriteObjectIdentifier("2.999.1");
            }
        }

        return Wrap("EC PRIVATE KEY", Convert.ToBase64String(writer.Encode()));
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
