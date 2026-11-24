using Microsoft.AspNetCore.DataProtection;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Provides controlled data protection results and failures for ticket store tests.</summary>
internal sealed class TicketStoreTestProtector(Func<byte[], byte[]> protect, Func<byte[], byte[]> unprotect) : IDataProtector
{
    /// <inheritdoc />
    public IDataProtector CreateProtector(string purpose) => this;

    /// <inheritdoc />
    public byte[] Protect(byte[] plaintext) => protect(plaintext);

    /// <inheritdoc />
    public byte[] Unprotect(byte[] protectedData) => unprotect(protectedData);
}
