using System.Security.Cryptography;
using System.Text;

namespace PandaBear.VaultManager.Tool.Tests.Attachments;

internal static class AttachmentSamples
{
    // Key markers are assembled at runtime so the repository holds no private-key-shaped literals.
    private const string Dashes = "-----";

    private static byte[] GeneratedPem()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return Encoding.UTF8.GetBytes(key.ExportPkcs8PrivateKeyPem());
    }

    public static TheoryData<string, byte[], string> All => new()
    {
        { "notes.txt", Encoding.UTF8.GetBytes("plain text\nsecond line"), "Text" },
        { "key.pem", GeneratedPem(), "PrivateKeyText" },
        { "id_ed25519", Encoding.UTF8.GetBytes(Dashes + "BEGIN OPENSSH " + "PRIVATE KEY" + Dashes + "\nAAAA\n"), "PrivateKeyText" },
        { "secret.asc", Encoding.UTF8.GetBytes(Dashes + "BEGIN PGP " + "PRIVATE KEY BLOCK" + Dashes + "\n\nAAAA\n"), "PrivateKeyText" },
        { "server.ppk", Encoding.UTF8.GetBytes("PuTTY-User-" + "Key-File-3: ssh-ed25519\nEncryption: none\n"), "PrivateKeyText" },
        { "ssh2.key", Encoding.UTF8.GetBytes("---- BEGIN SSH2 " + "ENCRYPTED PRIVATE KEY ----\nAAAA\n"), "PrivateKeyText" },
        { "vault.kdbx", [0x03, 0xD9, 0xA2, 0x9A, 0x67, 0xFB, 0x4B, 0xB5, 0x00, 0x00, 0x04, 0x00], "KeePass" },
        { "old.kdb", [0x03, 0xD9, 0xA2, 0x9A, 0x65, 0xFB, 0x4B, 0xB5, 0x00, 0x00], "KeePass" },
        { "cert.p12", [0x30, 0x82, 0x01, 0x00, 0x02, 0x01, 0x03, 0x30, 0x00], "Pkcs12" },
        { "cert.pfx", [0x01, 0x02, 0x03, 0x00], "Pkcs12" },
        { "inner.zip", TestZip.Create(("a.txt", [0x61])), "Zip" },
        { "data.db", [.. Encoding.ASCII.GetBytes("SQLite format 3\0"), 0x10, 0x00], "Sqlite" },
        { "wallet.dat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x00, 0x05, 0x31, 0x62, 0, 0], "BerkeleyDb" },
        { "wallet-le.dat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x62, 0x31, 0x05, 0x00, 0, 0], "BerkeleyDb" },
        {
            "UTC--keystore",
            Encoding.UTF8.GetBytes("""{"version":3,"id":"x","crypto":{"cipher":"aes-128-ctr","ciphertext":"00","kdf":"scrypt","kdfparams":{},"mac":"00"}}"""),
            "EthereumKeystore"
        },
        { "doc.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n\0\0binary"), "Pdf" },
        { "blob.bin", [0x00, 0xFF, 0x10, 0x00, 0x7F], "Binary" },
        { "latin1.txt", [0x63, 0x61, 0x66, 0xE9], "Binary" },
    };
}
