using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Sends page views to Umami through its documented <c>POST /api/send</c>, through the named <c>HttpClient</c> of
/// <see cref="UmamiPageViewBuilderExtensions.HttpClientName"/>, so the host's HTTP client defaults (resilience, timeouts, telemetry)
/// apply. Each page view is its own request because the visitor's <c>User-Agent</c> and address travel as request headers.
/// </summary>
/// <param name="name">The name of the target: its connection name.</param>
/// <param name="connection">Umami's address and the website id.</param>
/// <param name="clientIpHeader">The header that carries the visitor's address.</param>
/// <param name="httpClientFactory">The factory of the named Umami client.</param>
internal sealed class UmamiPageViewTarget(string name, UmamiConnection connection, string clientIpHeader, IHttpClientFactory httpClientFactory) : IPageViewTarget
{
    /// <inheritdoc />
    public string Name { get; } = name;

    /// <summary>Creates the target of <paramref name="connectionName"/> from its checked settings.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="connectionName">The connection name.</param>
    /// <returns>The target.</returns>
    /// <exception cref="OptionsValidationException">The connection string or the settings are invalid.</exception>
    public static UmamiPageViewTarget Create(IServiceProvider services, string connectionName)
    {
        var options = services.GetRequiredService<IOptionsMonitor<UmamiPageViewTargetOptions>>().Get(connectionName);
        var connection = UmamiConnection.Parse(options.ConnectionString)
            ?? throw new OptionsValidationException(connectionName, typeof(UmamiPageViewTargetOptions), [$"ConnectionStrings:{connectionName} is not an Umami connection string."]);
        return new UmamiPageViewTarget(connectionName, connection, options.ClientIpHeader, services.GetRequiredService<IHttpClientFactory>());
    }

    /// <inheritdoc />
    /// <exception cref="HttpRequestException">Umami answered with a status other than success, or the request failed.</exception>
    public async Task Send(PageView pageView, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pageView);

        using var request = Request(pageView);
        using var client = httpClientFactory.CreateClient(UmamiPageViewBuilderExtensions.HttpClientName);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage Request(PageView pageView)
    {
        var body = new UmamiSendRequest
        {
            Payload = new UmamiEventPayload
            {
                Website = connection.WebsiteId,
                Hostname = pageView.Hostname,
                Url = pageView.PathAndQuery,
                Referrer = pageView.Referrer,
                Language = pageView.Language,
            },
        };
        var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, UmamiJsonContext.Default.UmamiSendRequest));
        content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
        var request = new HttpRequestMessage(HttpMethod.Post, connection.SendAddress) { Content = content };
        request.Headers.TryAddWithoutValidation(HeaderNames.UserAgent, pageView.UserAgent);
        if (pageView.ClientIp is { } address)
        {
            request.Headers.TryAddWithoutValidation(clientIpHeader, address.ToString());
        }

        return request;
    }
}
