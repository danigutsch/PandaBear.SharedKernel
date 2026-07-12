using System.Net.Http.Json;
using SharedKernel.HttpClients;

namespace SharedKernel.Branding;

/// <summary>
/// HTTP client for authenticated Management Web Branding API contracts.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
public sealed class ManagementBrandingApiClient(HttpClient httpClient) : IManagementBrandingApiClient
{
    private const string ManagementSettingsRequestPath = $"/api/v1/{BrandingRoutes.ManagementSettingsPath}";
    private static readonly BrandingApiClientJsonContext Json = BrandingApiClientJsonContext.Default;

    /// <inheritdoc />
    public async Task<BrandingSettingsDto> GetSettings(CancellationToken ct)
    {
        var settings = await httpClient.GetFromJsonAsync(ManagementSettingsRequestPath, Json.BrandingSettingsDto, ct).ConfigureAwait(false);
        return settings ?? throw new InvalidOperationException("Branding API returned an empty settings response.");
    }

    /// <inheritdoc />
    public async Task<BrandingSettingsDto> SaveSettings(BrandingSettingsDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PutAsJsonAsync(ManagementSettingsRequestPath, request, Json.BrandingSettingsDto, ct).ConfigureAwait(false);
        await ContractHttpValidation.EnsureSuccessOrThrowValidationException(response, Json.ContractValidationProblemDto, ct).ConfigureAwait(false);

        var settings = await response.Content.ReadFromJsonAsync(Json.BrandingSettingsDto, ct).ConfigureAwait(false);
        return settings ?? throw new InvalidOperationException("Branding API returned an empty settings response.");
    }
}
