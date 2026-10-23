using System.Net.Http.Json;
using PandaBear.HttpClients;

namespace PandaBear.Branding;

/// <summary>
/// HTTP client for authenticated Management Web Branding API contracts.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
public sealed class ManagementBrandingApiClient(HttpClient httpClient) : IManagementBrandingApiClient
{
    private const int MaximumSettingsResponseBytes = 1024 * 1024;
    private const string ManagementSettingsRequestPath = $"/api/v1/{BrandingRoutes.ManagementSettingsPath}";
    private static readonly BrandingApiClientJsonContext Json = BrandingApiClientJsonContext.Default;

    /// <inheritdoc />
    public async Task<BrandingSettingsDto> GetSettings(CancellationToken ct)
    {
        var settings = await httpClient.GetFromJsonAsync(ManagementSettingsRequestPath, Json.BrandingSettingsDto, ct).ConfigureAwait(false);
        return settings ?? throw new InvalidOperationException("Branding API returned an empty settings response.");
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">The settings request is null.</exception>
    /// <exception cref="HttpRequestException">The response is unsuccessful or its success body exceeds 1 MiB or the smaller client buffer limit.</exception>
    /// <exception cref="ContractValidationException">The API returns an HTTP 400 validation response.</exception>
    /// <exception cref="System.Text.Json.JsonException">The successful response body is not valid settings JSON.</exception>
    /// <exception cref="InvalidOperationException">The API returns a null settings response.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels or the client timeout expires while sending or reading the body.</exception>
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

            var responseLimit = (int)Math.Min(httpClient.MaxResponseContentBufferSize, MaximumSettingsResponseBytes);
            if (response.Content.Headers.ContentLength > responseLimit)
            {
                throw new HttpRequestException("Branding API response body exceeds the maximum response bytes.");
            }

            using var source = await response.Content.ReadAsStreamAsync(requestToken).ConfigureAwait(false);
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                requestToken.ThrowIfCancellationRequested();
                var remaining = responseLimit - (int)body.Length;
                var bytesRead = await source.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, remaining + 1)),
                    requestToken).ConfigureAwait(false);
                requestToken.ThrowIfCancellationRequested();
                if (bytesRead == 0)
                {
                    break;
                }

                if (bytesRead > remaining)
                {
                    throw new HttpRequestException("Branding API response body exceeds the maximum response bytes.");
                }

                await body.WriteAsync(buffer.AsMemory(0, bytesRead), requestToken).ConfigureAwait(false);
            }

            body.Position = 0;
            using var boundedContent = new StreamContent(body);
            boundedContent.Headers.ContentType = response.Content.Headers.ContentType;
            var settings = await boundedContent.ReadFromJsonAsync(Json.BrandingSettingsDto, requestToken).ConfigureAwait(false);
            return settings ?? throw new InvalidOperationException("Branding API returned an empty settings response.");
        }
        catch (OperationCanceledException exception) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(exception.Message, exception, ct);
        }
    }
}
