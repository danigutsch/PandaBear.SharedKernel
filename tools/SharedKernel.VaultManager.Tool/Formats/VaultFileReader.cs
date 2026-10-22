using System.Globalization;
using System.Text;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Formats.Keeper;

namespace PandaBear.VaultManager.Tool.Formats;

/// <summary>Reads any supported vault export file after detecting its format from content.</summary>
internal static class VaultFileReader
{
    /// <summary>Largest input file accepted, checked before the file is read.</summary>
    public const long MaxInputBytes = 256L * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Reads a vault export.</summary>
    /// <param name="path">Export file.</param>
    /// <param name="passwordProvider">Returns the file password for a password-protected export, given its file name.</param>
    /// <returns>Items and warnings.</returns>
    /// <exception cref="InvalidDataException">The file is too large, unsupported, malformed, account-restricted, or the password is wrong.</exception>
    public static VaultReadResult Read(string path, Func<string, string> passwordProvider)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(passwordProvider);

        byte[] content = ReadCapped(path);
        string fileName = Path.GetFileName(path);
        switch (FormatDetector.Detect(content))
        {
            case VaultFileFormat.KeeperJson:
                return KeeperJsonReader.Read(DecodeText(content), fileName);
            case VaultFileFormat.KeeperJsonZip:
                return KeeperJsonReader.ReadZip(path);
            case VaultFileFormat.KeeperCsv:
                return KeeperCsvReader.Read(DecodeText(content), fileName);
            case VaultFileFormat.BitwardenJson:
                return new VaultReadResult(BitwardenJsonReader.Read(DecodeText(content), fileName), []);
            case VaultFileFormat.BitwardenJsonPasswordProtected:
                string json = PasswordProtectedExport.Decrypt(DecodeText(content), passwordProvider(fileName));
                return new VaultReadResult(BitwardenJsonReader.Read(json, fileName), []);
            case VaultFileFormat.BitwardenJsonAccountRestricted:
                throw new InvalidDataException(
                    "This Bitwarden export is account-restricted and can only be decrypted by the Bitwarden account that created it. "
                    + "Export again as .json or as password-protected .json (Encrypted).");
            case VaultFileFormat.BitwardenCsv:
                return BitwardenCsvReader.Read(DecodeText(content), fileName);
            case VaultFileFormat.BitwardenZip:
                return BitwardenZipReader.Read(path);
            case VaultFileFormat.Unknown:
            default:
                throw new InvalidDataException(
                    "File format not recognized. Supported: Keeper JSON, zip, or CSV; Bitwarden JSON, password-protected JSON, CSV, or zip.");
        }
    }

    /// <summary>Reads a whole file after checking its size against <see cref="MaxInputBytes"/>.</summary>
    /// <param name="path">File to read.</param>
    /// <returns>File bytes.</returns>
    /// <exception cref="InvalidDataException">The file is larger than the cap.</exception>
    public static byte[] ReadCapped(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxInputBytes)
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                $"Input file is too large; the limit is {MaxInputBytes} bytes."));
        }

        byte[] content = new byte[stream.Length];
        stream.ReadExactly(content);
        return content;
    }

    /// <summary>Decodes strict UTF-8 text and drops a byte order mark.</summary>
    /// <param name="content">File bytes.</param>
    /// <returns>Text.</returns>
    /// <exception cref="InvalidDataException">The bytes are not valid UTF-8.</exception>
    public static string DecodeText(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        ReadOnlySpan<byte> span = content;
        if (span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            span = span[3..];
        }

        try
        {
            return StrictUtf8.GetString(span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("File is not valid UTF-8 text.", exception);
        }
    }
}
