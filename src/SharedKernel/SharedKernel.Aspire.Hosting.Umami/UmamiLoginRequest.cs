using System.Text.Json.Serialization;

namespace Aspire.Hosting;

/// <summary>The body of Umami's <c>POST /api/auth/login</c> (Umami 3.4.1, <c>src/app/api/auth/login/schema.ts</c>). It holds a password, so it has no <c>ToString</c> that prints it.</summary>
internal sealed class UmamiLoginRequest
{
    /// <summary>Gets the user name.</summary>
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    /// <summary>Gets the password.</summary>
    [JsonPropertyName("password")]
    public required string Password { get; init; }
}
