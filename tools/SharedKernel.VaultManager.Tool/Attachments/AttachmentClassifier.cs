using System.Text;
using System.Text.Json;
using System.Text.Unicode;

namespace PandaBear.VaultManager.Tool.Attachments;

/// <summary>Detects the content kind of an attachment from its first bytes, then from its file extension.</summary>
internal static class AttachmentClassifier
{
    /// <summary>Bytes from the start of a file that classification needs.</summary>
    public const int HeadLength = 8 * 1024;

    /// <summary>Classifies content from its first bytes.</summary>
    /// <param name="head">Up to <see cref="HeadLength"/> bytes from the start of the file.</param>
    /// <param name="complete">Whether <paramref name="head"/> is the whole file.</param>
    /// <param name="fileName">File name, used only when no magic bytes match.</param>
    /// <returns>Detected kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <see langword="null"/>.</exception>
    public static AttachmentKind Classify(ReadOnlySpan<byte> head, bool complete, string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        if (head.Length > HeadLength)
        {
            head = head[..HeadLength];
            complete = false;
        }

        if (MatchesMagic(head) is { } magic)
        {
            return magic;
        }

        if (IsUtf8Text(head, complete))
        {
            return ClassifyText(head);
        }

        return Path.GetExtension(fileName).ToUpperInvariant() switch
        {
            ".P12" or ".PFX" => AttachmentKind.Pkcs12,
            ".KDBX" or ".KDB" => AttachmentKind.KeePass,
            _ => AttachmentKind.Binary,
        };
    }

    /// <summary>Whether a kind holds text that may be extracted and scanned.</summary>
    /// <param name="kind">Content kind.</param>
    /// <returns><see langword="true"/> for text kinds; binary kinds are never scanned.</returns>
    public static bool IsText(AttachmentKind kind) =>
        kind is AttachmentKind.Text or AttachmentKind.PrivateKeyText or AttachmentKind.EthereumKeystore;

    private static AttachmentKind? MatchesMagic(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith((ReadOnlySpan<byte>)[0x03, 0xD9, 0xA2, 0x9A])
            && head.Length >= 8
            && head[4] is 0x67 or 0x65
            && head[5..8].SequenceEqual((ReadOnlySpan<byte>)[0xFB, 0x4B, 0xB5]))
        {
            return AttachmentKind.KeePass;
        }

        if (head.StartsWith("PK\x03\x04"u8))
        {
            return AttachmentKind.Zip;
        }

        if (head.StartsWith("SQLite format 3\0"u8))
        {
            return AttachmentKind.Sqlite;
        }

        // Berkeley DB B-tree magic at offset 12, in either byte order.
        if (head.Length >= 16
            && (head[12..16].SequenceEqual((ReadOnlySpan<byte>)[0x00, 0x05, 0x31, 0x62])
                || head[12..16].SequenceEqual((ReadOnlySpan<byte>)[0x62, 0x31, 0x05, 0x00])))
        {
            return AttachmentKind.BerkeleyDb;
        }

        if (head.StartsWith("%PDF"u8))
        {
            return AttachmentKind.Pdf;
        }

        return IsPkcs12(head) ? AttachmentKind.Pkcs12 : null;
    }

    // PFX ::= SEQUENCE { version INTEGER (3), ... } in DER: 30 <length> 02 01 03.
    private static bool IsPkcs12(ReadOnlySpan<byte> head)
    {
        if (head.Length < 2 || head[0] != 0x30)
        {
            return false;
        }

        int lengthOctets = head[1] < 0x80 ? 0 : head[1] - 0x80;
        int versionOffset = 2 + lengthOctets;
        return lengthOctets <= 4
            && head.Length >= versionOffset + 3
            && head.Slice(versionOffset, 3).SequenceEqual((ReadOnlySpan<byte>)[0x02, 0x01, 0x03]);
    }

    private static bool IsUtf8Text(ReadOnlySpan<byte> head, bool complete)
    {
        if (head.Contains((byte)0))
        {
            return false;
        }

        if (Utf8.IsValid(head))
        {
            return true;
        }

        // A head cut from a longer file may end inside a multi-byte character.
        for (int cut = 1; !complete && cut <= 3 && cut < head.Length; cut++)
        {
            if (Utf8.IsValid(head[..^cut]))
            {
                return true;
            }
        }

        return false;
    }

    private static AttachmentKind ClassifyText(ReadOnlySpan<byte> head)
    {
        string text = Encoding.UTF8.GetString(head);
        if (ContainsPrivateKeyMarker(text))
        {
            return AttachmentKind.PrivateKeyText;
        }

        return IsEthereumKeystore(head) ? AttachmentKind.EthereumKeystore : AttachmentKind.Text;
    }

    private static bool ContainsPrivateKeyMarker(string text)
    {
        int begin = text.IndexOf("-----BEGIN ", StringComparison.Ordinal);
        if (begin >= 0)
        {
            int end = text.IndexOf("-----", begin + 11, StringComparison.Ordinal);
            if (end > begin && text.AsSpan(begin, end - begin).Contains("PRIVATE KEY", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return text.Contains("PuTTY-User-Key-File-", StringComparison.Ordinal)
            || (text.Contains("---- BEGIN SSH2 ", StringComparison.Ordinal) && text.Contains("PRIVATE KEY ----", StringComparison.Ordinal));
    }

    private static bool IsEthereumKeystore(ReadOnlySpan<byte> head)
    {
        int start = head.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? 3 : 0;
        while (start < head.Length && head[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            start++;
        }

        if (start >= head.Length || head[start] != '{')
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(head[start..].ToArray());
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out JsonElement version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out int number)
                || number != 3)
            {
                return false;
            }

            JsonElement crypto = root.TryGetProperty("crypto", out JsonElement lower) ? lower
                : root.TryGetProperty("Crypto", out JsonElement upper) ? upper : default;
            return crypto.ValueKind == JsonValueKind.Object
                && crypto.TryGetProperty("kdf", out _)
                && crypto.TryGetProperty("ciphertext", out _)
                && crypto.TryGetProperty("mac", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
