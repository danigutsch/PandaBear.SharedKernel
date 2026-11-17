using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Aspire.Hosting;

/// <summary>
/// The three Umami 3.4.1 routes that set up a website, checked against the Umami source of that version: <c>POST /api/auth/login</c>
/// (<c>src/app/api/auth/login/route.ts</c>), <c>GET /api/websites/{id}</c>, which answers <c>null</c> for an unknown website
/// (<c>src/app/api/websites/[websiteId]/route.ts</c>), and <c>POST /api/websites</c>, which takes the id (<c>src/app/api/websites/route.ts</c>).
/// </summary>
/// <param name="client">The HTTP client.</param>
/// <param name="endpoint">Umami's base address.</param>
internal sealed class UmamiApiClient(HttpClient client, Uri endpoint)
{
    /// <summary>The user that Umami creates on its first start and that provisioning signs in as.</summary>
    public const string AdminUsername = "admin";

    private readonly Uri _base = endpoint.AbsolutePath.EndsWith('/') ? endpoint : new Uri(endpoint.AbsoluteUri + "/");

    /// <summary>Signs in as <see cref="AdminUsername"/> and creates the website with its id unless a website with that id exists.</summary>
    /// <param name="password">The admin password.</param>
    /// <param name="websiteId">The website id.</param>
    /// <param name="name">The website name.</param>
    /// <param name="domain">The website's domain.</param>
    /// <param name="ct">Cancels the requests.</param>
    /// <returns>Whether the website was created or existed.</returns>
    /// <exception cref="UmamiProvisioningException">A request failed or Umami refused it.</exception>
    public async Task<UmamiWebsiteOutcome> EnsureWebsite(string password, Guid websiteId, string name, string domain, CancellationToken ct)
    {
        var token = await SignIn(password, ct).ConfigureAwait(false);
        using var lookup = Request(HttpMethod.Get, $"api/websites/{websiteId:D}", token);
        var existing = await Send(lookup, UmamiProvisioningStep.Lookup, UmamiApiJsonContext.Default.UmamiWebsiteReply, ct).ConfigureAwait(false);
        if (existing?.Id == websiteId)
        {
            return UmamiWebsiteOutcome.AlreadyExists;
        }

        using var create = Request(HttpMethod.Post, "api/websites", token);
        create.Content = JsonContent.Create(
            new UmamiCreateWebsiteRequest { Id = websiteId, Name = name, Domain = domain },
            UmamiApiJsonContext.Default.UmamiCreateWebsiteRequest);
        await Send(create, UmamiProvisioningStep.Create, UmamiApiJsonContext.Default.UmamiWebsiteReply, ct).ConfigureAwait(false);
        return UmamiWebsiteOutcome.Created;
    }

    private async Task<string> SignIn(string password, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, "api/auth/login", token: null);
        request.Content = JsonContent.Create(
            new UmamiLoginRequest { Username = AdminUsername, Password = password },
            UmamiApiJsonContext.Default.UmamiLoginRequest);
        var login = await Send(request, UmamiProvisioningStep.SignIn, UmamiApiJsonContext.Default.UmamiLoginResponse, ct).ConfigureAwait(false);
        if (login?.RequiresTwoFactor == true)
        {
            throw new UmamiProvisioningException(UmamiProvisioningStep.SignIn, "the admin user signs in with a second factor, which provisioning cannot give");
        }

        return login?.Token is { Length: > 0 } token
            ? token
            : throw new UmamiProvisioningException(UmamiProvisioningStep.SignIn, "Umami returned no token");
    }

    private HttpRequestMessage Request(HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, new Uri(_base, path));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private async Task<T?> Send<T>(HttpRequestMessage request, UmamiProvisioningStep step, JsonTypeInfo<T> answer, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new UmamiProvisioningException(step, exception.GetType().Name);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new UmamiProvisioningException(step, (int)response.StatusCode);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync(answer, ct).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                throw new UmamiProvisioningException(step, "Umami's answer was not the expected JSON");
            }
        }
    }
}
