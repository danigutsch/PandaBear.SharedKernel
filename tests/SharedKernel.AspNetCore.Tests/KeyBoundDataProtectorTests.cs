using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>
/// Verifies opaque storage-entry keys bind protected values to their original entry.
/// </summary>
[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class KeyBoundDataProtectorTests
{
    [Fact]
    public void Round_trips_a_value_for_its_original_key()
    {
        // Arrange
        var protector = new KeyBoundDataProtector(new EphemeralDataProtectionProvider(), "test-purpose");
        var plaintext = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var protectedData = protector.Protect("entry-a", plaintext);
        var unprotectedData = protector.Unprotect("entry-a", protectedData);

        // Assert
        protectedData.SequenceEqual(plaintext).ShouldBeFalse();
        unprotectedData.SequenceEqual(plaintext).ShouldBeTrue();
    }

    [Fact]
    public void Rejects_a_value_protected_for_a_different_key()
    {
        // Arrange
        var protector = new KeyBoundDataProtector(new EphemeralDataProtectionProvider(), "test-purpose");
        var protectedData = protector.Protect("entry-a", [0x01]);

        // Act
        Action unprotect = () => protector.Unprotect("entry-b", protectedData);

        // Assert
        unprotect.ShouldThrow<CryptographicException>();
    }

    [Fact]
    public void Rejects_a_value_protected_for_a_different_purpose()
    {
        // Arrange
        var provider = new EphemeralDataProtectionProvider();
        var sourceProtector = new KeyBoundDataProtector(provider, "source-purpose");
        var targetProtector = new KeyBoundDataProtector(provider, "target-purpose");
        var protectedData = sourceProtector.Protect("entry-a", [0x01]);

        // Act
        Action unprotect = () => targetProtector.Unprotect("entry-a", protectedData);

        // Assert
        unprotect.ShouldThrow<CryptographicException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_blank_entry_keys(string key)
    {
        // Arrange
        var protector = new KeyBoundDataProtector(new EphemeralDataProtectionProvider(), "test-purpose");

        // Act
        Action protect = () => protector.Protect(key, [0x01]);

        // Assert
        protect.ShouldThrow<ArgumentException>();
    }
}
