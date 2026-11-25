namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// How the client authenticates to the token and revocation endpoints.
/// </summary>
public enum TokenEndpointClientAuthentication
{
    /// <summary>
    /// <c>client_secret_post</c>: the client identifier and secret are form fields, as the OpenID Connect handler sends them
    /// when it redeems the authorization code.
    /// </summary>
    PostBody = 0,

    /// <summary>
    /// <c>client_secret_basic</c>: the form-encoded client identifier and secret are sent in a Basic Authorization header.
    /// </summary>
    BasicHeader = 1
}
