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
    /// <exception cref="IOException">
    /// The file does not exist, is a directory, or cannot be read, also when access to it is denied.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="passwordProvider"/> is <see langword="null"/>.</exception>
    public static VaultReadResult Read(string path, Func<string, string> passwordProvider)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(passwordProvider);

        // Opening a directory fails as access denied, which would hide the real mistake.
        if (Directory.Exists(path))
        {
            throw new IOException($"Input file '{Path.GetFullPath(path)}' is a directory. Choose an export file.");
        }

        try
        {
            return ReadDetected(path, passwordProvider);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new IOException($"Input file '{Path.GetFullPath(path)}' cannot be read because access to it is denied.", exception);
        }
    }

    private static VaultReadResult ReadDetected(string path, Func<string, string> passwordProvider)
    {
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
                List<string> warnings = [];
                return new VaultReadResult(BitwardenJsonReader.Read(DecodeText(content), fileName, warnings), warnings);
            case VaultFileFormat.BitwardenJsonPasswordProtected:
                string json = PasswordProtectedExport.Decrypt(DecodeText(content), passwordProvider(fileName));
                List<string> protectedWarnings = [];
                return new VaultReadResult(BitwardenJsonReader.Read(json, fileName, protectedWarnings), protectedWarnings);
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
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
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
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
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
