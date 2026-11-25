using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>Builds unsigned JWTs whose payload the token manager reads without validating.</summary>
internal static class UnsignedJwt
{
    /// <summary>Creates an unsigned JWT with the given issuer and subject.</summary>
    /// <param name="issuer">The <c>iss</c> claim.</param>
    /// <param name="subject">The <c>sub</c> claim.</param>
    /// <returns>The compact JWT.</returns>
    internal static string Create(string issuer, string subject)
    {
        return string.Concat(
            Encode(new Dictionary<string, string> { ["alg"] = "none" }),
            ".",
            Encode(new Dictionary<string, string> { ["iss"] = issuer, ["sub"] = subject }),
            ".");
    }

    private static string Encode(Dictionary<string, string> value)
    {
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
    }
}
