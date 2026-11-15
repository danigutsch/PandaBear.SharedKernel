using System.Text.Json.Serialization;

namespace Aspire.Hosting;

/// <summary>The part of Umami's sign-in answer that provisioning reads (Umami 3.4.1, <c>src/app/api/auth/login/route.ts</c>).</summary>
internal sealed class UmamiLoginResponse
{
    /// <summary>Gets the bearer token, absent when a second factor is required.</summary>
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    /// <summary>Gets a value indicating whether the user signs in with a second factor, which provisioning cannot give.</summary>
    [JsonPropertyName("requiresTwoFactor")]
    public bool RequiresTwoFactor { get; init; }
}
