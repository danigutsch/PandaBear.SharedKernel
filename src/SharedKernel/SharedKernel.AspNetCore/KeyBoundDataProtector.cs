using Microsoft.AspNetCore.DataProtection;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Protects values with a purpose derived from an opaque storage-entry key.
/// </summary>
public sealed class KeyBoundDataProtector
{
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyBoundDataProtector" /> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The provider used to create key-bound protectors.</param>
    /// <param name="purpose">The stable application purpose for the protected value type.</param>
    public KeyBoundDataProtector(IDataProtectionProvider dataProtectionProvider, string purpose)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        _protector = dataProtectionProvider.CreateProtector(purpose);
    }

    /// <summary>
    /// Protects a value for one opaque storage-entry key.
    /// </summary>
    /// <param name="key">The storage-entry key that must also be supplied to unprotect the value.</param>
    /// <param name="plaintext">The value to protect.</param>
    /// <returns>The protected value.</returns>
    public byte[] Protect(string key, byte[] plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(plaintext);

        return GetProtector(key).Protect(plaintext);
    }

    /// <summary>
    /// Unprotects a value for the opaque storage-entry key that protected it.
    /// </summary>
    /// <param name="key">The storage-entry key used when the value was protected.</param>
    /// <param name="protectedData">The protected value.</param>
    /// <returns>The original plaintext value.</returns>
    public byte[] Unprotect(string key, byte[] protectedData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(protectedData);

        return GetProtector(key).Unprotect(protectedData);
    }

    private IDataProtector GetProtector(string key)
    {
        return _protector.CreateProtector(key);
    }
}
