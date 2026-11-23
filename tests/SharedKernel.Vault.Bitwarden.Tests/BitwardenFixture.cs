using PandaBear.Vault.Model;

namespace PandaBear.Vault.Bitwarden.Tests;

/// <summary>Reads the Bitwarden sample vault that is copied next to the test assembly.</summary>
internal static class BitwardenFixture
{
    /// <summary>Returns the vault that the Bitwarden JSON reader produces from the bitwarden/full.json fixture.</summary>
    public static VaultExport ReadFull() => BitwardenJsonReader.Read(Fixture.Read("bitwarden/full.json"), "full.json");
}
