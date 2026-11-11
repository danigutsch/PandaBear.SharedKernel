using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Reads the user of the current HTTP request.
/// </summary>
internal sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    /// <inheritdoc />
    public ValueTask<ClaimsPrincipal> GetCurrentUser(CancellationToken ct)
    {
        return ValueTask.FromResult(httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity()));
    }
}
