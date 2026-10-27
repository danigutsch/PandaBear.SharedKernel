using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests;

/// <summary>Loads the sample vault files that are copied next to the test assembly.</summary>
internal static class Fixture
{
    /// <summary>Returns the text of a file below the output Fixtures folder.</summary>
    /// <param name="relativePath">The path relative to the Fixtures folder.</param>
    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath));

    /// <summary>Returns the vault that the Bitwarden JSON reader produces from the bitwarden/full.json fixture.</summary>
    public static VaultExport ReadBitwardenFull() => BitwardenJsonReader.Read(Read("bitwarden/full.json"), "full.json");
}
