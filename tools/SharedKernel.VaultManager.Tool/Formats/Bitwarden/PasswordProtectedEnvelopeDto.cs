using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden password-protected export wrapper.</summary>
internal sealed record PasswordProtectedEnvelopeDto
{
    public bool Encrypted
    {
        get; init;
    }

    public bool PasswordProtected
    {
        get; init;
    }

    public string? Salt
    {
        get; init;
    }

    public int KdfType
    {
        get; init;
    }

    public int KdfIterations
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? KdfMemory
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? KdfParallelism
    {
        get; init;
    }

    [JsonPropertyName("encKeyValidation_DO_NOT_EDIT")]
    public string? EncKeyValidation
    {
        get; init;
    }

    public string? Data
    {
        get; init;
    }
}
