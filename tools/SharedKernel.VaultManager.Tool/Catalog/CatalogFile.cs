using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>
/// Reads and writes the versioned catalog file:
/// <c>{"version":1,"source":{...},"folders":[...],"items":[...]}</c>. A property the format does not define, a property
/// given twice in one object, and a missing property are rejected. Text is written as typed, with control and format
/// characters escaped.
/// </summary>
internal static class CatalogFile
{
    /// <summary>Format version written by this tool, and the only version it reads.</summary>
    public const int CurrentVersion = 1;

    // Names and notes keep accented letters and apostrophes as typed, so the file stays readable when edited, as in the
    // decisions file.
    private static readonly JsonTypeInfo<CatalogDocument> ReadableDocument = (JsonTypeInfo<CatalogDocument>)new JsonSerializerOptions(CatalogJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }.GetTypeInfo(typeof(CatalogDocument));

    /// <summary>Reads a catalog file.</summary>
    /// <param name="path">Catalog file.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="InvalidDataException">
    /// The file is not a valid catalog of a supported version. The message starts with the full path of the file and
    /// names the JSON path of the problem, never a value.
    /// </exception>
    /// <exception cref="IOException">The file does not exist or cannot be read.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public static CatalogDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string json = File.ReadAllText(path);
        try
        {
            return Deserialize(json);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException(Path.GetFullPath(path) + ": " + exception.Message, exception);
        }
    }

    /// <summary>Writes a catalog to a file readable and writable only by the current user on Unix.</summary>
    /// <param name="path">Catalog file.</param>
    /// <param name="catalog">Catalog.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <exception cref="IOException">The file cannot be written.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    public static void Write(string path, CatalogDocument catalog, bool overwrite)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(catalog);

        SecureFile.WriteAllText(path, Serialize(catalog), overwrite);
    }

    /// <summary>Serializes a catalog.</summary>
    /// <param name="catalog">Catalog.</param>
    /// <returns>Indented JSON with text as typed, except for escaped control and format characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    public static string Serialize(CatalogDocument catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return DecisionsFile.EscapeFormatCharacters(JsonSerializer.Serialize(catalog, ReadableDocument));
    }

    /// <summary>Parses and validates a catalog.</summary>
    /// <param name="json">Catalog file text.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="InvalidDataException">
    /// The text is not a valid catalog of a supported version, or two items have the same key. The message names the JSON
    /// path of the problem or the key, never a value.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    public static CatalogDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        CatalogDocument? catalog;
        try
        {
            catalog = JsonSerializer.Deserialize(json, CatalogJsonContext.Default.CatalogDocument);
        }
        catch (JsonException exception)
        {
            // The parser's message can quote the text around the problem, so only its location is reported.
            long line = (exception.LineNumber ?? 0) + 1;
            throw new InvalidDataException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The catalog is not valid at {exception.Path ?? "$"} (line {line}). Look there for a misspelled, unknown, missing, or repeated property, a value of the wrong type, or broken JSON syntax."),
                exception);
        }

        if (catalog is null)
        {
            throw new InvalidDataException("The catalog is empty.");
        }

        if (catalog.Version != CurrentVersion)
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"The catalog has version {catalog.Version}; only version {CurrentVersion} is supported."));
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (CatalogItem item in catalog.Items)
        {
            if (!keys.Add(item.Key))
            {
                throw new InvalidDataException($"The catalog has two items with the key {item.Key}.");
            }
        }

        return catalog;
    }

    /// <summary>Computes the SHA-256 of a file, as <see cref="CatalogSource.Sha256"/> records it.</summary>
    /// <param name="path">File.</param>
    /// <returns>Lowercase hexadecimal digest.</returns>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public static string Hash(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
