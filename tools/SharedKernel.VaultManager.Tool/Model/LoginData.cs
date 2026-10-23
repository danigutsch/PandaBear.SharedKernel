using System.Text;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Login credentials of a vault item.</summary>
internal sealed record LoginData
{
    /// <summary>User name.</summary>
    public string? Username
    {
        get; init;
    }

    /// <summary>Password.</summary>
    public string? Password
    {
        get; init;
    }

    /// <summary>Authenticator key: an <c>otpauth://</c> URI, <c>steam://</c> value, or base32 secret.</summary>
    public string? Totp
    {
        get; init;
    }

    /// <summary>Website addresses.</summary>
    public IReadOnlyList<LoginUri> Uris { get; init; } = [];

    /// <summary>Stored passkeys.</summary>
    public IReadOnlyList<Fido2Credential> Fido2Credentials { get; init; } = [];

    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Uris = ").Append(Uris.Count).Append(", Fido2Credentials = ").Append(Fido2Credentials.Count);
        return true;
    }
}
