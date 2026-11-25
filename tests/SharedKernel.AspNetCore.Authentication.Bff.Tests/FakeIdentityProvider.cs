using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>A fake identity provider token, exchange and revocation endpoint that records every request.</summary>
internal sealed class FakeIdentityProvider : HttpMessageHandler
{
    /// <summary>The token endpoint of the fake.</summary>
    internal static readonly Uri TokenEndpoint = new("https://idp.test/realms/test/token");
    /// <summary>The revocation endpoint of the fake.</summary>
    internal static readonly Uri RevocationEndpoint = new("https://idp.test/realms/test/revoke");

    private int _issued;

    /// <summary>Requests received, in order.</summary>
    internal ConcurrentQueue<IdentityProviderRequest> Requests { get; } = new();

    /// <summary>Delay before each answer.</summary>
    internal TimeSpan Delay { get; set; }

    /// <summary>Lifetime in seconds of issued tokens.</summary>
    internal int ExpiresIn { get; set; } = 300;

    /// <summary>Whether a refresh answer carries a new refresh token.</summary>
    internal bool RotateRefreshTokens { get; set; } = true;

    /// <summary>Overrides the answer to a request; returning null falls back to the default answer.</summary>
    internal Func<IdentityProviderRequest, HttpResponseMessage?>? Respond { get; set; }

    /// <summary>The requests with the given grant type.</summary>
    internal IReadOnlyList<IdentityProviderRequest> Grants(string grantType)
    {
        return [.. Requests.Where(request => request.Form.GetValueOrDefault("grant_type") == grantType)];
    }

    /// <summary>Creates a JSON answer.</summary>
    internal static HttpResponseMessage Json(HttpStatusCode status, object body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var form = QueryHelpers.ParseQuery(body).ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
        var recorded = new IdentityProviderRequest(request.RequestUri!, request.Headers.Authorization?.ToString(), form);
        Requests.Enqueue(recorded);

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (Respond?.Invoke(recorded) is { } answer)
        {
            return answer;
        }

        return DefaultAnswer(recorded);
    }

    private HttpResponseMessage DefaultAnswer(IdentityProviderRequest request)
    {
        if (request.Uri == RevocationEndpoint)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        var number = Interlocked.Increment(ref _issued);
        return request.Form.GetValueOrDefault("grant_type") switch
        {
            "refresh_token" => Json(HttpStatusCode.OK, RotateRefreshTokens
                ? new Dictionary<string, object>
                {
                    ["access_token"] = $"access-{number}",
                    ["token_type"] = "Bearer",
                    ["expires_in"] = ExpiresIn,
                    ["refresh_token"] = $"refresh-{number}"
                }
                : new Dictionary<string, object>
                {
                    ["access_token"] = $"access-{number}",
                    ["token_type"] = "Bearer",
                    ["expires_in"] = ExpiresIn
                }),
            "urn:ietf:params:oauth:grant-type:token-exchange" => Json(HttpStatusCode.OK, new Dictionary<string, object>
            {
                ["access_token"] = $"exchanged-{request.Form["audience"]}-{number}",
                ["token_type"] = "Bearer",
                ["issued_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
                ["expires_in"] = ExpiresIn
            }),
            _ => Json(HttpStatusCode.BadRequest, new Dictionary<string, object> { ["error"] = "unsupported_grant_type" })
        };
    }
}
