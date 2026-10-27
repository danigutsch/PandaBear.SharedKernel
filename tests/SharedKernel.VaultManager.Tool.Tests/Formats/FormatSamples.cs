namespace PandaBear.VaultManager.Tool.Tests.Formats;

/// <summary>Writes small sample files in each supported and unsupported vault format into a directory.</summary>
internal static class FormatSamples
{
    /// <summary>An encrypted Bitwarden JSON export that carries an account key validation entry, so only its own account can open it.</summary>
    public const string AccountRestricted = """{"encrypted":true,"encKeyValidation_DO_NOT_EDIT":"2.a|b|c","folders":[],"items":[]}""";

    /// <summary>Writes a text file into a directory.</summary>
    /// <param name="directory">The existing directory to write into.</param>
    /// <param name="name">The file name.</param>
    /// <param name="text">The file content.</param>
    /// <returns>The path of the file.</returns>
    public static string Write(string directory, string name, string text)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Writes a binary file into a directory.</summary>
    /// <param name="directory">The existing directory to write into.</param>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">The file content.</param>
    /// <returns>The path of the file.</returns>
    public static string Write(string directory, string name, byte[] bytes)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Writes the named sample file, such as keeper-json, bitwarden-zip, or other-text, into a directory.</summary>
    /// <param name="directory">The existing directory to write into.</param>
    /// <param name="sample">The sample name.</param>
    /// <returns>The path of the file.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The sample name is not known.</exception>
    public static string WriteSample(string directory, string sample) => sample switch
    {
        "keeper-json" => Write(directory, "keeper.json", Fixture.Read("keeper/commander.json")),
        "keeper-array" => Write(directory, "keeper-array.json", """[{"uid":"a","title":"t"}]"""),
        "keeper-zip" => Write(directory, "keeper.zip", TestZip.Create(("export.json", TestZip.Utf8("""{"records":[]}""")))),
        "keeper-csv" => Write(directory, "keeper.csv", Fixture.Read("keeper/web.csv")),
        "bitwarden-json" => Write(directory, "bitwarden.json", Fixture.Read("bitwarden/full.json")),
        "bitwarden-protected" => Write(directory, "protected.json", Fixture.Read("bitwarden/protected-pbkdf2.json")),
        "bitwarden-restricted" => Write(directory, "restricted.json", AccountRestricted),
        "bitwarden-csv" => Write(directory, "bitwarden.csv", Fixture.Read("bitwarden-csv/individual.csv")),
        "bitwarden-org-csv" => Write(directory, "org.csv", Fixture.Read("bitwarden-csv/organization.csv")),
        "bitwarden-zip" => Write(directory, "bitwarden.zip", TestZip.Create(("data.json", TestZip.Utf8("""{"encrypted":false,"items":[]}""")))),
        "other-zip" => Write(directory, "other.zip", TestZip.Create(("readme.txt", [0x61]))),
        "other-json" => Write(directory, "other.json", """{"hello":"world"}"""),
        "other-text" => Write(directory, "other.txt", "just some text"),
        _ => throw new ArgumentOutOfRangeException(nameof(sample)),
    };
}
