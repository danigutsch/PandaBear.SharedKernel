using System.Net.Http.Headers;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Exchanges the current user's access token for one audience (RFC 8693) and attaches the result as a Bearer Authorization
/// header.
/// </summary>
internal sealed class TokenExchangeHandler(
    ICurrentUserAccessor currentUser,
    UserTokenManager tokens,
    TokenExchangeTarget target) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await currentUser.GetCurrentUser(cancellationToken).ConfigureAwait(false);
        var result = await tokens.GetAudienceAccessToken(user, target.Audience, target.Scope, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new UserTokenUnavailableException(result.Failure);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
