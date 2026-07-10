using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using SharedKernel.InputNormalization;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Provides reusable CORS policy helpers for ASP.NET Core applications.
/// </summary>
public static class AspNetCoreCorsExtensions
{
    /// <summary>
    /// Adds a CORS policy from a configuration section containing allowed origins.
    /// </summary>
    /// <remarks>
    /// Empty sections create a deny-all policy. Applications still own the policy name and the
    /// configuration path to keep security boundaries explicit at the composition root.
    /// </remarks>
    /// <param name="options">The CORS options to configure.</param>
    /// <param name="policyName">The application-owned CORS policy name.</param>
    /// <param name="allowedOriginsSection">A configuration section whose children contain allowed origins.</param>
    /// <returns>The same <see cref="CorsOptions"/> instance.</returns>
    public static CorsOptions AddConfiguredOriginsPolicy(
        this CorsOptions options,
        string policyName,
        IConfiguration allowedOriginsSection)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(allowedOriginsSection);

        var allowedOrigins = GetAllowedOrigins(allowedOriginsSection);

        options.AddPolicy(policyName, policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
            else
            {
                policy.SetIsOriginAllowed(_ => false);
            }
        });

        return options;
    }

    private static string[] GetAllowedOrigins(IConfiguration configuration)
    {
        return StringSanitizer.SanitizeCollection(configuration.GetChildren()
            .Select(static section => section.Value)
            .OfType<string>());
    }
}
