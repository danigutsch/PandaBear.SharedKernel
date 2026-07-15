using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace SharedKernel.AspNetCore.Tests;

internal static class ApiAuthenticationTokenFactory
{
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
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
