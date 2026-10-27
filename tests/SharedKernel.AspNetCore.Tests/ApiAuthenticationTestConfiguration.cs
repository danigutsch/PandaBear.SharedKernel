using Microsoft.Extensions.Configuration;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Builds in-memory configuration for the API bearer-authentication tests.</summary>
internal static class ApiAuthenticationTestConfiguration
{
    /// <summary>Creates a configuration holding the authority, issuer and HTTP-development-authority settings under their <see cref="ApiAuthenticationDefaults" /> keys.</summary>
    /// <param name="authority">The identity provider authority URL.</param>
    /// <param name="issuer">The expected token issuer.</param>
    /// <param name="allowHttpDevelopmentAuthority">Whether an HTTP authority is allowed in Development.</param>
    /// <returns>The built configuration.</returns>
    public static IConfiguration Create(
        string authority,
        string issuer,
        bool allowHttpDevelopmentAuthority = false)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ApiAuthenticationDefaults.AuthorityConfigurationKey] = authority,
                [ApiAuthenticationDefaults.IssuerConfigurationKey] = issuer,
                [ApiAuthenticationDefaults.AllowHttpDevelopmentAuthorityConfigurationKey] = allowHttpDevelopmentAuthority.ToString()
            })
            .Build();
    }
}
