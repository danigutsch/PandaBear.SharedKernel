using System.Net.Http.Json;

namespace SharedKernel.Branding;

/// <summary>
/// HTTP client for branding API contracts.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
public sealed class BrandingApiClient(HttpClient httpClient) : IBrandingApiClient
{
    private const string ApiRoutePrefix = "/api/v1";
    private const string PublicSettingsRequestPath = $"{ApiRoutePrefix}/{BrandingRoutes.PublicSettingsPath}";
    private static readonly BrandingApiClientJsonContext Json = BrandingApiClientJsonContext.Default;

    /// <inheritdoc />
    public async Task<BrandingSettingsDto> GetPublicSettings(CancellationToken ct)
    {
        var settings = await httpClient.GetFromJsonAsync(PublicSettingsRequestPath, Json.BrandingSettingsDto, ct).ConfigureAwait(false);
        return settings ?? throw new InvalidOperationException("Branding API returned an empty settings response.");
    }

}
