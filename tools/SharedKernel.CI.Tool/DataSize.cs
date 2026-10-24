using System.Globalization;

namespace PandaBear.CI.Tool;

/// <summary>Formats byte counts in messages.</summary>
internal static class DataSize
{
    private const long Mebibyte = 1024 * 1024;

    /// <summary>Formats whole mebibytes as <c>N MiB</c> and any other count as <c>N bytes</c>.</summary>
    public static string Format(long bytes) =>
        bytes % Mebibyte == 0
            ? (bytes / Mebibyte).ToString(CultureInfo.InvariantCulture) + " MiB"
            : bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
}
