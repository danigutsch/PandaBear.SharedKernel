using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Calls the token and revocation endpoints named by the OpenID Connect discovery document. Tokens, error descriptions and
/// response bodies never reach a log; only the operation, the HTTP status and a well-formed OAuth error code do.
/// </summary>
internal sealed partial class IdentityProviderClient
{
    /// <summary>The operation name logged for a refresh_token grant.</summary>
    internal const string RefreshOperation = "refresh";

    /// <summary>The operation name logged for a token exchange.</summary>
    internal const string ExchangeOperation = "token-exchange";

    /// <summary>The operation name logged for a revocation.</summary>
    internal const string RevokeOperation = "revocation";

    private const string AccessTokenType = "urn:ietf:params:oauth:token-type:access_token";
    private const string TokenExchangeGrantType = "urn:ietf:params:oauth:grant-type:token-exchange";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<OpenIdConnectOptions> _openIdConnectOptions;
    private readonly UserTokenManagementOptions _options;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="IdentityProviderClient" /> class.</summary>
    /// <param name="httpClientFactory">Creates the identity provider client.</param>
    /// <param name="openIdConnectOptions">Supplies the client credentials and discovery document.</param>
    /// <param name="options">The token management options.</param>
    /// <param name="logger">The logger; it never receives a token.</param>
    internal IdentityProviderClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> openIdConnectOptions,
        UserTokenManagementOptions options,
        ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _openIdConnectOptions = openIdConnectOptions;
        _options = options;
        _logger = logger;
    }

    /// <summary>Redeems a refresh token (RFC 6749 section 6).</summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    internal Task<TokenEndpointOutcome> Refresh(string refreshToken, CancellationToken ct)
    {
        return CallTokenEndpoint(
            RefreshOperation,
            [new("grant_type", "refresh_token"), new("refresh_token", refreshToken)],
            ct);
    }

    /// <summary>Exchanges an access token for a token of another audience (RFC 8693 section 2.1).</summary>
    /// <param name="subjectToken">The access token to exchange.</param>
    /// <param name="target">The audience and scope to request.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    internal Task<TokenEndpointOutcome> Exchange(string subjectToken, TokenExchangeTarget target, CancellationToken ct)
    {
        List<KeyValuePair<string, string>> fields =
        [
            new("grant_type", TokenExchangeGrantType),
            new("subject_token", subjectToken),
            new("subject_token_type", AccessTokenType),
            new("requested_token_type", AccessTokenType),
            new("audience", target.Audience)
        ];
        if (target.Scope is not null)
        {
            fields.Add(new("scope", target.Scope));
        }

        return CallTokenEndpoint(ExchangeOperation, fields, ct);
    }

    /// <summary>Revokes a refresh token (RFC 7009) when the discovery document names a revocation endpoint.</summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>Whether the identity provider confirmed the revocation; <see langword="null" /> when it has no endpoint.</returns>
    internal async Task<bool?> Revoke(string refreshToken, CancellationToken ct)
    {
        var (client, configuration) = await ResolveEndpoints(RevokeOperation, ct).ConfigureAwait(false);
        if (client is null || configuration is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(configuration.RevocationEndpoint))
        {
            return null;
        }

        var endpoint = ValidateEndpoint(configuration.RevocationEndpoint, client);
        if (endpoint is null)
        {
            LogEndpointInvalid(_logger, RevokeOperation);
            return false;
        }

        var outcome = await Send(
            RevokeOperation,
            endpoint,
            client,
            [new("token", refreshToken), new("token_type_hint", "refresh_token")],
            expectAnswer: false,
            ct).ConfigureAwait(false);
        return outcome.Answer is not null;
    }

    private async Task<TokenEndpointOutcome> CallTokenEndpoint(
        string operation,
        IReadOnlyList<KeyValuePair<string, string>> fields,
        CancellationToken ct)
    {
        var (client, configuration) = await ResolveEndpoints(operation, ct).ConfigureAwait(false);
        if (client is null || configuration is null)
        {
            return TokenEndpointOutcome.Unavailable();
        }

        var endpoint = ValidateEndpoint(configuration.TokenEndpoint, client);
        if (endpoint is null)
        {
            LogEndpointInvalid(_logger, operation);
            return TokenEndpointOutcome.Unavailable();
        }

        return await Send(operation, endpoint, client, fields, expectAnswer: true, ct).ConfigureAwait(false);
    }

    private async Task<(OpenIdConnectOptions? Client, OpenIdConnectConfiguration? Configuration)> ResolveEndpoints(
        string operation,
        CancellationToken ct)
    {
        var client = _openIdConnectOptions.Get(_options.OpenIdConnectScheme);
        if (string.IsNullOrWhiteSpace(client.ClientId))
        {
            LogClientMissing(_logger, operation);
            return (null, null);
        }

        if (client.Configuration is { } configuration)
        {
            return (client, configuration);
        }

        if (client.ConfigurationManager is null)
        {
            LogEndpointInvalid(_logger, operation);
            return (null, null);
        }

        try
        {
            return (client, await client.ConfigurationManager.GetConfigurationAsync(ct).ConfigureAwait(false));
        }
        catch (InvalidOperationException exception)
        {
            LogDiscoveryFailed(_logger, operation, exception.GetType().Name);
            return (null, null);
        }
        catch (HttpRequestException exception)
        {
            LogDiscoveryFailed(_logger, operation, exception.GetType().Name);
            return (null, null);
        }
    }

    private static Uri? ValidateEndpoint(string? value, OpenIdConnectOptions client)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            && (endpoint.Scheme == Uri.UriSchemeHttps || (!client.RequireHttpsMetadata && endpoint.Scheme == Uri.UriSchemeHttp))
            ? endpoint
            : null;
    }

    private async Task<TokenEndpointOutcome> Send(
        string operation,
        Uri endpoint,
        OpenIdConnectOptions client,
        IReadOnlyList<KeyValuePair<string, string>> fields,
        bool expectAnswer,
        CancellationToken ct)
    {
        var form = new List<KeyValuePair<string, string>>(fields);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_options.ClientAuthentication == TokenEndpointClientAuthentication.BasicHeader && client.ClientSecret is not null)
        {
            // RFC 6749 section 2.3.1: the identifier and secret are form-encoded before they are joined and Base64-encoded.
            var credentials = string.Concat(Uri.EscapeDataString(client.ClientId!), ":", Uri.EscapeDataString(client.ClientSecret));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        }
        else
        {
            form.Add(new("client_id", client.ClientId!));
            if (client.ClientSecret is not null)
            {
                form.Add(new("client_secret", client.ClientSecret));
            }
        }

        request.Content = new FormUrlEncodedContent(form);
        var httpClient = _httpClientFactory.CreateClient(UserTokenManagementDefaults.IdentityProviderHttpClientName);
        try
        {
            using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return expectAnswer ? Parse(operation, body) : TokenEndpointOutcome.Granted(new TokenEndpointAnswer());
            }

            var error = ReadErrorCode(body);
            LogRejected(_logger, operation, (int)response.StatusCode, error ?? "none");
            return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized && error is not null
                ? TokenEndpointOutcome.Rejection()
                : TokenEndpointOutcome.Unavailable();
        }
        catch (HttpRequestException exception)
        {
            LogUnavailable(_logger, operation, exception.GetType().Name);
            return TokenEndpointOutcome.Unavailable();
        }
        catch (TaskCanceledException exception) when (!ct.IsCancellationRequested)
        {
            LogUnavailable(_logger, operation, exception.InnerException is TimeoutException ? "Timeout" : exception.GetType().Name);
            return TokenEndpointOutcome.Unavailable();
        }
    }

    private TokenEndpointOutcome Parse(string operation, byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                LogUnreadable(_logger, operation);
                return TokenEndpointOutcome.Unavailable();
            }

            return TokenEndpointOutcome.Granted(new TokenEndpointAnswer
            {
                AccessToken = ReadString(root, "access_token"),
                TokenType = ReadString(root, "token_type"),
                ExpiresIn = ReadLifetime(root),
                RefreshToken = ReadString(root, "refresh_token"),
                IdentityToken = ReadString(root, "id_token"),
                Scope = ReadString(root, "scope"),
                IssuedTokenType = ReadString(root, "issued_token_type")
            });
        }
        catch (JsonException)
        {
            LogUnreadable(_logger, operation);
            return TokenEndpointOutcome.Unavailable();
        }
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static long? ReadLifetime(JsonElement root)
    {
        if (!root.TryGetProperty("expires_in", out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var seconds) => seconds,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) => seconds,
            _ => null
        };
    }

    /// <summary>Reads the OAuth <c>error</c> code, keeping it only when it uses the RFC 6749 character set of known codes.</summary>
    private static string? ReadErrorCode(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var error = document.RootElement.ValueKind == JsonValueKind.Object ? ReadString(document.RootElement, "error") : null;
            return error is { Length: > 0 and <= 64 } && error.All(character => character is (>= 'a' and <= 'z') or '_')
                ? error
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(LogLevel.Warning, "Identity provider {Operation} call was rejected. Status: {StatusCode}. Error: {Error}.")]
    private static partial void LogRejected(ILogger logger, string operation, int statusCode, string error);

    [LoggerMessage(LogLevel.Warning, "Identity provider {Operation} call failed. Failure type: {FailureType}.")]
    private static partial void LogUnavailable(ILogger logger, string operation, string failureType);

    [LoggerMessage(LogLevel.Warning, "Identity provider {Operation} answer could not be read.")]
    private static partial void LogUnreadable(ILogger logger, string operation);

    [LoggerMessage(LogLevel.Error, "Identity provider {Operation} endpoint is missing or not an allowed absolute URI.")]
    private static partial void LogEndpointInvalid(ILogger logger, string operation);

    [LoggerMessage(LogLevel.Error, "Identity provider {Operation} call has no client identifier configured.")]
    private static partial void LogClientMissing(ILogger logger, string operation);

    [LoggerMessage(LogLevel.Warning, "Identity provider discovery for {Operation} failed. Failure type: {FailureType}.")]
    private static partial void LogDiscoveryFailed(ILogger logger, string operation, string failureType);
}
