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

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Put,
            new Uri(ManagementSettingsRequestPath, UriKind.Relative))
        {
            Content = JsonContent.Create(request, Json.BrandingSettingsDto)
        };
        using var timeoutSource = httpClient.Timeout == Timeout.InfiniteTimeSpan
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource?.CancelAfter(httpClient.Timeout);
        var requestToken = timeoutSource?.Token ?? ct;

        try
        {
            using var response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                requestToken).ConfigureAwait(false);
            await ContractHttpValidation.EnsureSuccessOrThrowValidationException(
                response,
                Json.ContractValidationProblemDto,
                requestToken).ConfigureAwait(false);

            var settings = await response.Content.ReadFromJsonAsync(Json.BrandingSettingsDto, requestToken).ConfigureAwait(false);
            return settings ?? throw new InvalidOperationException("Branding API returned an empty settings response.");
        }
        catch (OperationCanceledException exception) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(exception.Message, exception, ct);
        }
    }
}
