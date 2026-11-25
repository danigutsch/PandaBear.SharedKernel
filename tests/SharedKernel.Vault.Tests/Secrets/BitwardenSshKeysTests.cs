using SharedKernel.Vault.Secrets;

namespace SharedKernel.Vault.Tests.Secrets;

/// <summary>Tests that SSH keys become Bitwarden SSH key data only when they fit Bitwarden's field budget.</summary>
public sealed class BitwardenSshKeysTests
{
    [Fact]
    public void A_key_larger_than_the_bitwarden_field_budget_is_flagged_not_converted()
    {
        // Arrange
        string pem = OpenSshContainer.Unencrypted("ssh-ed25519", 6000);

        // Act
        var inspection = BitwardenSshKeys.Inspect(pem);
        var conversion = BitwardenSshKeys.Convert(pem);

        // Assert
        inspection.CanConvert.ShouldBeFalse();
        conversion.Key.ShouldBeNull();
        conversion.Problem.ShouldBe("key exceeds the Bitwarden field budget");
    }

    [Fact]
    public void A_key_that_fits_becomes_ssh_key_data_with_its_public_key_and_fingerprint()
    {
        // Act
        var conversion = BitwardenSshKeys.Convert(PemFixtures.Ed25519OpenSsh(), PemFixtures.Ed25519PublicKey);

        // Assert
        conversion.Problem.ShouldBeNull();
        var key = conversion.Key.ShouldNotBeNull();
        key.PublicKey.ShouldBe(PemFixtures.Ed25519PublicKey);
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
    }
}
