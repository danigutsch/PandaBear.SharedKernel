using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden login object.</summary>
internal sealed record BitwardenLoginDto
{
    public IReadOnlyList<BitwardenUriDto>? Uris { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? Totp { get; init; }

    public IReadOnlyList<Fido2Credential>? Fido2Credentials { get; init; }
}
