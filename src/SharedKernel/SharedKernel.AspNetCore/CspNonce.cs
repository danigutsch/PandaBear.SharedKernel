using System.Security.Cryptography;

namespace PandaBear.AspNetCore;

/// <summary>Creates the nonce of one scope from <see cref="RandomNumberGenerator" /> on first read.</summary>
internal sealed class CspNonce : ICspNonce
{
    private const int SizeInBytes = 16;

    private readonly Lazy<string> _value = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <inheritdoc />
    public string Value => _value.Value;

    private static string Create()
    {
        Span<byte> bytes = stackalloc byte[SizeInBytes];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
