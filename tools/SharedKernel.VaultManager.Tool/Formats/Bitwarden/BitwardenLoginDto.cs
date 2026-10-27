using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden login object.</summary>
internal sealed record BitwardenLoginDto
{
    /// <summary>URIs the login applies to.</summary>
    public IReadOnlyList<BitwardenUriDto>? Uris
    {
        get; init;
    }

    /// <summary>Login user name.</summary>
    public string? Username
    {
        get; init;
    }

    /// <summary>Login password.</summary>
    public string? Password
    {
        get; init;
    }

    /// <summary>TOTP secret or otpauth URI for one-time codes.</summary>
    public string? Totp
    {
        get; init;
    }

    /// <summary>Passkey credentials stored with the login.</summary>
    public IReadOnlyList<Fido2Credential>? Fido2Credentials
    {
        get; init;
    }
}
