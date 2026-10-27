using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Creates signed JWTs for the bearer audience-validation tests.</summary>
internal static class ApiAuthenticationTokenFactory
{
    /// <summary>Creates an RS256-signed token valid from one minute ago for five minutes, with the given issuer and one <c>aud</c> claim per audience.</summary>
    /// <param name="signingKey">The RSA key that signs the token.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="audiences">The audience values, possibly empty.</param>
    /// <returns>The serialized token.</returns>
    public static string Create(RsaSecurityKey signingKey, string issuer, IReadOnlyCollection<string> audiences)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentNullException.ThrowIfNull(audiences);

        var claims = audiences.Select(static audience => new Claim(JwtRegisteredClaimNames.Aud, audience));
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: null,
            claims: claims,
            notBefore: TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(-1),
            expires: TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(5),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
