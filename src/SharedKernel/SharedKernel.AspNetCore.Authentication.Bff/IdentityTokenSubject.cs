using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Reads the <c>iss</c> and <c>sub</c> claims of an ID token's payload without validating it, to compare a refreshed ID
/// token with the one from the login (OpenID Connect Core 1.0 section 12.2).
/// </summary>
internal static class IdentityTokenSubject
{
    /// <summary>Returns whether two ID tokens name the same issuer and subject.</summary>
    /// <param name="original">The stored ID token.</param>
    /// <param name="refreshed">The ID token of a refresh answer.</param>
    /// <returns><see langword="true" /> when both payloads carry equal <c>iss</c> and <c>sub</c> claims.</returns>
    internal static bool Matches(string original, string refreshed)
    {
        return Read(original) is { } before && Read(refreshed) is { } after && before == after;
    }

    private static (string Issuer, string Subject)? Read(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(WebEncoders.Base64UrlDecode(parts[1]));
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("iss", out var issuer) && issuer.ValueKind == JsonValueKind.String
                && root.TryGetProperty("sub", out var subject) && subject.ValueKind == JsonValueKind.String
                ? (issuer.GetString()!, subject.GetString()!)
                : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }
}
