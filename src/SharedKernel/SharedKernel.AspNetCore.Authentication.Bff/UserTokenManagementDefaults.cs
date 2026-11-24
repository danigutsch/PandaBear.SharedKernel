namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Names used by the user token management services.
/// </summary>
public static class UserTokenManagementDefaults
{
    /// <summary>
    /// The named <see cref="HttpClient" /> that calls the identity provider's token and revocation endpoints.
    /// </summary>
    /// <remarks>
    /// Configure its primary handler, for example a proxy, through <c>services.AddHttpClient(IdentityProviderHttpClientName)</c>.
    /// Additional handlers such as retry policies are removed from it, because a replayed refresh_token grant can revoke a
    /// rotated refresh token.
    /// </remarks>
    public const string IdentityProviderHttpClientName = "SharedKernel.AspNetCore.Authentication.Bff.IdentityProvider";
}
