using System.Security.Claims;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Finds the user on whose behalf an outgoing HTTP call is made.
/// </summary>
/// <remarks>
/// The default reads the current HTTP request. <see cref="UserTokenManagementServiceCollectionExtensions.AddBlazorServerCurrentUser" />
/// replaces it with one that reads the Blazor Server circuit, where no HTTP request is current.
/// </remarks>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// Returns the current user, or an unauthenticated principal when there is none.
    /// </summary>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The current user.</returns>
    ValueTask<ClaimsPrincipal> GetCurrentUser(CancellationToken ct);
}
