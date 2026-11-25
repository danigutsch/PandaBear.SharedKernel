using System.Globalization;
using System.Text;
using SharedKernel.Vault.Bitwarden;
using SharedKernel.Vault.Formats;
using SharedKernel.Vault.Keeper;

namespace SharedKernel.Vault.Import;

/// <summary>Reads any supported vault export file after detecting its format from content.</summary>
public sealed class VaultFileReader : IVaultExportReader
{
    /// <summary>Largest input file accepted, checked before the file is read.</summary>
    public const long MaxInputBytes = 256L * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private VaultFileReader()
    {
    }

    /// <summary>Gets a stateless reader for the built-in providers.</summary>
    public static VaultFileReader Instance { get; } = new();

    /// <summary>Detects a supported export and checks a protected envelope before asking for any password.</summary>
    /// <param name="path">Export file.</param>
    /// <returns>The detected format.</returns>
    /// <exception cref="InvalidDataException">The file is unsupported, oversized, or has an invalid protected envelope.</exception>
    /// <exception cref="IOException">The input cannot be read.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    public static VaultFileFormat CheckInput(string path)
    {
        byte[] content = ReadCapped(path);
        VaultFileFormat format = FormatDetector.Detect(content);
        if (format is VaultFileFormat.Unknown or VaultFileFormat.BitwardenJsonAccountRestricted)
        {
            _ = ReadDetected(path, content, format, static _ => string.Empty);
        }
        else if (format == VaultFileFormat.BitwardenJsonPasswordProtected)
        {
            PasswordProtectedExport.CheckEnvelope(DecodeText(content));
        }

        return format;
    }

    /// <summary>Reads a vault export.</summary>
    /// <param name="path">Export file.</param>
    /// <param name="passwordProvider">Returns the file password for a password-protected export, given its file name.</param>
    /// <returns>Items and warnings.</returns>
    /// <exception cref="InvalidDataException">The file is too large, unsupported, malformed, account-restricted, or the password is wrong.</exception>
    /// <exception cref="IOException">
    /// The file does not exist, is a directory, or cannot be read, also when access to it is denied.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="passwordProvider"/> is <see langword="null"/>.</exception>
    public VaultReadResult Read(string path, Func<string, string> passwordProvider)
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
            byte[] content = ReadCapped(path);
            return ReadDetected(path, content, FormatDetector.Detect(content), passwordProvider);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new IOException($"Input file '{Path.GetFullPath(path)}' cannot be read because access to it is denied.", exception);
        }
    }

    private static VaultReadResult ReadDetected(string path, byte[] content, VaultFileFormat format, Func<string, string> passwordProvider)
    {
        string fileName = Path.GetFileName(path);
        switch (format)
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
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    internal static byte[] ReadCapped(string path)
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <see langword="null"/>.</exception>
    internal static string DecodeText(byte[] content)
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
