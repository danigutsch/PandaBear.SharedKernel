using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Converts private keys into Bitwarden SSH key data with the BCL only. Bitwarden accepts OpenSSH and PKCS#8 keys; PKCS#1
/// and SEC1 keys are rewritten as PKCS#8. Encrypted, PuTTY, DSA, and security-key (<c>sk-</c>) keys are flagged and never
/// converted, and so are keys that cannot be read, for example because the body is not base64 or the ECDSA curve is not
/// <c>nistp256</c>, <c>nistp384</c>, or <c>nistp521</c>. Fingerprints use the <c>SHA256:</c> form printed by
/// <c>ssh-keygen -lf</c> and stored by Bitwarden.
/// </summary>
internal static class SshKeyConverter
{
    private const string RsaOid = "1.2.840.113549.1.1.1";
    private const string EcOid = "1.2.840.10045.2.1";
    private const string Ed25519Oid = "1.3.101.112";
    private const string DsaOid = "1.2.840.10040.4.1";
    private const string BudgetProblem = "key exceeds the Bitwarden field budget";
    private const string BodyProblem = "key body is not base64";
    private const string CurveProblem = "unsupported ECDSA curve";

    /// <summary>
    /// Determines format, algorithm, and encryption without converting, and whether <see cref="Convert"/> can build an SSH
    /// key from the key. An ECDSA key can be converted only when its SEC1 or PKCS#8 structure names <c>nistp256</c>,
    /// <c>nistp384</c>, or <c>nistp521</c>; a key with explicit curve parameters names no curve.
    /// </summary>
    /// <param name="pem">Private key text.</param>
    /// <returns>The inspection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pem"/> is <see langword="null"/>.</exception>
    public static SshKeyInspection Inspect(string pem)
    {
        ArgumentNullException.ThrowIfNull(pem);
        if (pem.Contains("PuTTY-User-Key-File-", StringComparison.Ordinal))
        {
            return new SshKeyInspection("PuTTY", null, pem.Contains("Encryption: aes", StringComparison.Ordinal), false, "PuTTY keys are not converted");
        }

        if (pem.Contains("---- BEGIN SSH2 ENCRYPTED PRIVATE KEY ----", StringComparison.Ordinal))
        {
            return new SshKeyInspection("SSH2", null, true, false, "encrypted keys are not converted");
        }

        if (!PemBlock.TryParse(pem, out PemBlock? block) || block is null)
        {
            return new SshKeyInspection("unknown", null, false, false, "not a complete PEM block");
        }

        SshKeyInspection inspection = InspectBlock(block);
        if (inspection.CanConvert && !BitwardenLimits.Fits(pem, BitwardenLimits.ValueEncryptedLength))
        {
            return inspection with { CanConvert = false, Problem = BudgetProblem };
        }

        return inspection;
    }

    /// <summary>Converts a private key. A key that is malformed or unsupported yields a problem instead of an exception.</summary>
    /// <param name="pem">Private key text.</param>
    /// <param name="suppliedPublicKey">Matching public key in authorized_keys form, used for Ed25519 PKCS#8 keys.</param>
    /// <returns>Converted key data, or the problem that prevented conversion.</returns>
    public static SshKeyConversion Convert(string pem, string? suppliedPublicKey = null)
    {
        ArgumentNullException.ThrowIfNull(pem);
        SshKeyInspection inspection = Inspect(pem);
        if (!inspection.CanConvert)
        {
            return new SshKeyConversion(null, inspection.Problem);
        }

        PemBlock block = PemBlock.TryParse(pem, out PemBlock? parsed) && parsed is not null
            ? parsed
            : throw new InvalidOperationException("Inspected key is no longer parseable.");
        try
        {
            SshKeyConversion conversion = block.Label switch
            {
                "OPENSSH PRIVATE KEY" => ConvertOpenSsh(pem, block),
                "RSA PRIVATE KEY" => ConvertRsa(block),
                "EC PRIVATE KEY" => ConvertSec1(block),
                _ => ConvertPkcs8(pem, block, suppliedPublicKey),
            };
            return conversion.Key?.PrivateKey is { } privateKey && !BitwardenLimits.Fits(privateKey, BitwardenLimits.ValueEncryptedLength)
                ? new SshKeyConversion(null, BudgetProblem)
                : conversion;
        }
        catch (CryptographicException)
        {
            return new SshKeyConversion(null, "key could not be read");
        }
        catch (AsnContentException)
        {
            return new SshKeyConversion(null, "key structure is malformed");
        }
        catch (PlatformNotSupportedException)
        {
            // Importing an ECDSA key throws this when the key names a curve the platform does not implement.
            return new SshKeyConversion(null, CurveProblem);
        }
    }

    /// <summary>OpenSSH SHA-256 fingerprint of a public key blob.</summary>
    /// <param name="blob">Public key in SSH wire format.</param>
    /// <returns><c>SHA256:</c> followed by unpadded base64.</returns>
    public static string Fingerprint(ReadOnlySpan<byte> blob)
    {
        return "SHA256:" + System.Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');
    }

    private static SshKeyInspection InspectBlock(PemBlock block)
    {
        switch (block.Label)
        {
            case "ENCRYPTED PRIVATE KEY":
                return new SshKeyInspection("PKCS#8", null, true, false, "encrypted keys are not converted");
            case "DSA PRIVATE KEY":
                return new SshKeyInspection("PKCS#1", "DSA", block.HasEncryptedHeader, false, "DSA keys are not supported by Bitwarden");
            case "RSA PRIVATE KEY":
            case "EC PRIVATE KEY":
                string algorithm = block.Label.StartsWith("RSA", StringComparison.Ordinal) ? "RSA" : "ECDSA";
                if (block.HasEncryptedHeader)
                {
                    return new SshKeyInspection("PKCS#1", algorithm, true, false, "encrypted keys are not converted");
                }

                if (algorithm == "ECDSA")
                {
                    return InspectSec1(block);
                }

                return HasBase64Body(block)
                    ? new SshKeyInspection("PKCS#1", algorithm, false, true, null)
                    : new SshKeyInspection("PKCS#1", algorithm, false, false, BodyProblem);
            case "PRIVATE KEY":
                return InspectPkcs8(block);
            case "OPENSSH PRIVATE KEY":
                return InspectOpenSsh(block);
            default:
                return new SshKeyInspection(block.Label, null, false, false, "unsupported key format");
        }
    }

    private static SshKeyInspection InspectPkcs8(PemBlock block)
    {
        if (!SecretText.TryDecodeBase64(block.Body, out byte[]? der) || der is null)
        {
            return new SshKeyInspection("PKCS#8", null, false, false, BodyProblem);
        }

        try
        {
            Pkcs8Info info = ReadPkcs8(der);
            return info.AlgorithmOid switch
            {
                RsaOid => new SshKeyInspection("PKCS#8", "RSA", false, true, null),
                EcOid => SshCurveName(info.CurveOid) is null
                    ? new SshKeyInspection("PKCS#8", "ECDSA", false, false, CurveProblem)
                    : new SshKeyInspection("PKCS#8", "ECDSA", false, true, null),
                Ed25519Oid => new SshKeyInspection("PKCS#8", "Ed25519", false, true, null),
                DsaOid => new SshKeyInspection("PKCS#8", "DSA", false, false, "DSA keys are not supported by Bitwarden"),
                _ => new SshKeyInspection("PKCS#8", null, false, false, "unsupported key algorithm"),
            };
        }
        catch (AsnContentException)
        {
            return new SshKeyInspection("PKCS#8", null, false, false, "key structure is malformed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    // A SEC1 key names its curve in its optional [0] parameters. Explicit curve parameters, or none, name no curve.
    private static SshKeyInspection InspectSec1(PemBlock block)
    {
        if (!SecretText.TryDecodeBase64(block.Body, out byte[]? der) || der is null)
        {
            return new SshKeyInspection("PKCS#1", "ECDSA", false, false, BodyProblem);
        }

        try
        {
            var reader = new AsnReader(der, AsnEncodingRules.BER);
            AsnReader key = reader.ReadSequence();
            _ = key.ReadInteger();
            SkipPrivateKeyOctets(key);
            string? curveOid = null;
            if (key.HasData && key.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 0)))
            {
                curveOid = ReadCurveOid(key.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0)));
            }

            return SshCurveName(curveOid) is null
                ? new SshKeyInspection("PKCS#1", "ECDSA", false, false, CurveProblem)
                : new SshKeyInspection("PKCS#1", "ECDSA", false, true, null);
        }
        catch (AsnContentException)
        {
            return new SshKeyInspection("PKCS#1", "ECDSA", false, false, "key structure is malformed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    // Reads past the OCTET STRING that holds private key material without keeping a copy of it.
    private static void SkipPrivateKeyOctets(AsnReader reader)
    {
        if (!reader.TryReadPrimitiveOctetString(out _))
        {
            CryptographicOperations.ZeroMemory(reader.ReadOctetString());
        }
    }

    // ECParameters is a named curve OID, NULL, or explicit SpecifiedECDomain parameters; only the first names a curve.
    private static string? ReadCurveOid(AsnReader parameters) =>
        parameters.HasData && parameters.PeekTag().HasSameClassAndValue(Asn1Tag.ObjectIdentifier) ? parameters.ReadObjectIdentifier() : null;

    // SSH key types name these curves only; a key on another curve, or with explicit curve parameters, has no SSH form.
    private static string? SshCurveName(string? curveOid) => curveOid switch
    {
        "1.2.840.10045.3.1.7" => "nistp256",
        "1.3.132.0.34" => "nistp384",
        "1.3.132.0.35" => "nistp521",
        _ => null,
    };

    private static SshKeyInspection InspectOpenSsh(PemBlock block)
    {
        if (!TryReadOpenSsh(block, out OpenSshContainer? container) || container is null)
        {
            return new SshKeyInspection("OpenSSH", null, false, false, "key structure is malformed");
        }

        string keyType = container.KeyType;
        if (container.Cipher != "none")
        {
            return new SshKeyInspection("OpenSSH", keyType, true, false, "encrypted keys are not converted");
        }

        if (keyType.StartsWith("sk-", StringComparison.Ordinal))
        {
            return new SshKeyInspection("OpenSSH", keyType, false, false, "security key (sk-) keys are not supported by Bitwarden");
        }

        if (keyType == "ssh-dss")
        {
            return new SshKeyInspection("OpenSSH", keyType, false, false, "DSA keys are not supported by Bitwarden");
        }

        if (keyType is not ("ssh-ed25519" or "ssh-rsa" or "ecdsa-sha2-nistp256" or "ecdsa-sha2-nistp384" or "ecdsa-sha2-nistp521"))
        {
            return new SshKeyInspection("OpenSSH", keyType, false, false, "unsupported key algorithm");
        }

        return container.ChecksMatch
            ? new SshKeyInspection("OpenSSH", keyType, false, true, null)
            : new SshKeyInspection("OpenSSH", keyType, false, false, "key structure is malformed");
    }

    private static SshKeyConversion ConvertOpenSsh(string pem, PemBlock block)
    {
        _ = TryReadOpenSsh(block, out OpenSshContainer? container);
        byte[] blob = container!.PublicBlob;
        string privateKey = Pem(pem);
        return new SshKeyConversion(Data(privateKey, container.KeyType, blob), null);
    }

    // PKCS#1 and SEC1 keys are imported from the body that inspection found to be base64. Importing the PEM text throws
    // ArgumentException for base64 that PemEncoding rejects as non-canonical, such as nonzero padding bits.
    private static SshKeyConversion ConvertRsa(PemBlock block)
    {
        _ = SecretText.TryDecodeBase64(block.Body, out byte[]? der);
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(der, out _);
            return new SshKeyConversion(Data(rsa.ExportPkcs8PrivateKeyPem(), "ssh-rsa", RsaBlob(rsa)), null);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    private static SshKeyConversion ConvertSec1(PemBlock block)
    {
        _ = SecretText.TryDecodeBase64(block.Body, out byte[]? der);
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(der, out _);
            return EcdsaData(ecdsa, ecdsa.ExportPkcs8PrivateKeyPem());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    private static SshKeyConversion ConvertPkcs8(string pem, PemBlock block, string? suppliedPublicKey)
    {
        _ = SecretText.TryDecodeBase64(block.Body, out byte[]? der);
        try
        {
            Pkcs8Info info = ReadPkcs8(der!);
            string privateKey = Pem(pem);
            switch (info.AlgorithmOid)
            {
                case RsaOid:
                    using (var rsa = RSA.Create())
                    {
                        rsa.ImportPkcs8PrivateKey(der, out _);
                        return new SshKeyConversion(Data(privateKey, "ssh-rsa", RsaBlob(rsa)), null);
                    }

                case EcOid:
                    using (var ecdsa = ECDsa.Create())
                    {
                        ecdsa.ImportPkcs8PrivateKey(der, out _);
                        return EcdsaData(ecdsa, privateKey);
                    }

                default:
                    byte[]? blob = info.PublicKey is { Length: 32 } embedded
                        ? Blob(Encoding.ASCII.GetBytes("ssh-ed25519"), embedded)
                        : SuppliedEd25519Blob(suppliedPublicKey);
                    return blob is null
                        ? new SshKeyConversion(null, "Ed25519 PKCS#8 key has no public key; supply the matching ssh-ed25519 public key")
                        : new SshKeyConversion(Data(privateKey, "ssh-ed25519", blob), null);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    private static SshKeyConversion EcdsaData(ECDsa ecdsa, string privateKey)
    {
        ECParameters parameters = ecdsa.ExportParameters(false);

        // Inspection already turned away keys whose structure names no supported curve; the curve the platform reports is
        // checked as well. The OID is read null-safely because platforms differ in what they report for unnamed curves.
        string? curve = parameters.Curve.IsNamed ? SshCurveName(parameters.Curve.Oid?.Value) : null;
        if (curve is null)
        {
            return new SshKeyConversion(null, CurveProblem);
        }

        string keyType = "ecdsa-sha2-" + curve;
        byte[] point = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        byte[] blob = Blob(Encoding.ASCII.GetBytes(keyType), Encoding.ASCII.GetBytes(curve), point);
        return new SshKeyConversion(Data(privateKey, keyType, blob), null);
    }

    private static SshKeyData Data(string privateKey, string keyType, byte[] blob)
    {
        return new SshKeyData
        {
            PrivateKey = privateKey,
            PublicKey = keyType + " " + System.Convert.ToBase64String(blob),
            KeyFingerprint = Fingerprint(blob),
        };
    }

    private static byte[] RsaBlob(RSA rsa)
    {
        RSAParameters parameters = rsa.ExportParameters(false);
        return Blob(Encoding.ASCII.GetBytes("ssh-rsa"), Mpint(parameters.Exponent!), Mpint(parameters.Modulus!));
    }

    private static byte[]? SuppliedEd25519Blob(string? suppliedPublicKey)
    {
        string[] parts = suppliedPublicKey?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length < 2 || parts[0] != "ssh-ed25519" || !SecretText.TryDecodeBase64(parts[1], out byte[]? blob) || blob is null)
        {
            return null;
        }

        var reader = new SshWireReader(blob);
        return reader.TryReadString(out byte[] type) && Encoding.ASCII.GetString(type) == "ssh-ed25519"
            && reader.TryReadString(out byte[] key) && key.Length == 32 && reader.AtEnd
            ? blob
            : null;
    }

    private static byte[] Mpint(byte[] magnitude)
    {
        int start = 0;
        while (start < magnitude.Length - 1 && magnitude[start] == 0)
        {
            start++;
        }

        ReadOnlySpan<byte> trimmed = magnitude.AsSpan(start);
        return (trimmed[0] & 0x80) != 0 ? [0x00, .. trimmed] : trimmed.ToArray();
    }

    private static byte[] Blob(params byte[][] strings)
    {
        using var stream = new MemoryStream();
        Span<byte> length = stackalloc byte[4];
        foreach (byte[] value in strings)
        {
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)value.Length);
            stream.Write(length);
            stream.Write(value);
        }

        return stream.ToArray();
    }

    private static string Pem(string pem)
    {
        return pem.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }

    private static bool HasBase64Body(PemBlock block)
    {
        if (!SecretText.TryDecodeBase64(block.Body, out byte[]? der) || der is null)
        {
            return false;
        }

        CryptographicOperations.ZeroMemory(der);
        return true;
    }

    private static Pkcs8Info ReadPkcs8(byte[] der)
    {
        var reader = new AsnReader(der, AsnEncodingRules.BER);
        AsnReader sequence = reader.ReadSequence();
        _ = sequence.ReadInteger();
        AsnReader algorithm = sequence.ReadSequence();
        string oid = algorithm.ReadObjectIdentifier();
        string? curveOid = oid == EcOid ? ReadCurveOid(algorithm) : null;
        SkipPrivateKeyOctets(sequence);
        byte[]? publicKey = null;
        while (sequence.HasData)
        {
            Asn1Tag tag = sequence.PeekTag();
            if (tag.HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 1)))
            {
                publicKey = sequence.ReadBitString(out _, new Asn1Tag(TagClass.ContextSpecific, 1));
            }
            else
            {
                _ = sequence.ReadEncodedValue();
            }
        }

        return new Pkcs8Info(oid, curveOid, publicKey);
    }

    private static bool TryReadOpenSsh(PemBlock block, out OpenSshContainer? container)
    {
        container = null;
        if (!SecretText.TryDecodeBase64(block.Body, out byte[]? data) || data is null)
        {
            return false;
        }

        ReadOnlySpan<byte> magic = "openssh-key-v1\0"u8;
        if (!data.AsSpan().StartsWith(magic))
        {
            return false;
        }

        var reader = new SshWireReader(data.AsMemory(magic.Length));
        if (!reader.TryReadString(out byte[] cipher) || !reader.TryReadString(out _) || !reader.TryReadString(out _)
            || !reader.TryReadUInt32(out uint keyCount) || keyCount != 1
            || !reader.TryReadString(out byte[] publicBlob) || !reader.TryReadString(out byte[] privateSection))
        {
            return false;
        }

        var blobReader = new SshWireReader(publicBlob);
        if (!blobReader.TryReadString(out byte[] keyType))
        {
            return false;
        }

        bool checksMatch = privateSection.Length >= 8
            && privateSection.AsSpan(0, 4).SequenceEqual(privateSection.AsSpan(4, 4));
        CryptographicOperations.ZeroMemory(privateSection);
        CryptographicOperations.ZeroMemory(data);
        container = new OpenSshContainer(Encoding.ASCII.GetString(cipher), Encoding.ASCII.GetString(keyType), publicBlob, checksMatch);
        return true;
    }

    private sealed record Pkcs8Info(string AlgorithmOid, string? CurveOid, byte[]? PublicKey);

    private sealed record OpenSshContainer(string Cipher, string KeyType, byte[] PublicBlob, bool ChecksMatch);
}
