using Microsoft.Extensions.Configuration;

namespace SharedKernel.AspNetCore.Tests;

internal static class ApiAuthenticationTestConfiguration
{
    public static IConfiguration Create(string authority, string issuer, bool allowHttpDevelopmentAuthority = false)
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
