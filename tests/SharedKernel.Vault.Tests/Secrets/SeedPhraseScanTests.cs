using SharedKernel.SecretScanning;
using SharedKernel.Vault.Secrets;

namespace SharedKernel.Vault.Tests.Secrets;

/// <summary>Tests the validators that confirm a candidate secret: card numbers, Base32 seeds, BIP39 phrases, JWTs, token checksums, and age keys.</summary>
public sealed class SeedPhraseScanTests
{
    [Fact]
    public void Electrum_seed_version_prefix_is_recognized_when_bip39_checksum_fails()
    {
        // Arrange
        string phrase = Bip39Reference.ElectrumPhrase();

        // Act
        var findings = ItemScanner.Default.Scan(SecretTestItems.Note(phrase));

        // Assert
        var finding = findings.ShouldHaveSingleItem();
        finding.RuleId.ShouldBe("electrum-seed");
        finding.Confidence.ShouldBe(Confidence.High);
    }
}
