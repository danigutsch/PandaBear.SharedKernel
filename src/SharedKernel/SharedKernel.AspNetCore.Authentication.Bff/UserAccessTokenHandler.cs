using System.Net.Http.Headers;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Attaches the current user's access token, refreshed when needed, as a Bearer Authorization header.
/// </summary>
internal sealed class UserAccessTokenHandler(ICurrentUserAccessor currentUser, UserTokenManager tokens) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await currentUser.GetCurrentUser(cancellationToken).ConfigureAwait(false);
        var result = await tokens.GetAccessToken(user, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new UserTokenUnavailableException(result.Failure);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
