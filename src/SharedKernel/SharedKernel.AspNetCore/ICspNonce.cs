namespace SharedKernel.AspNetCore;

/// <summary>
/// The Content-Security-Policy nonce of the current request: a CSP3 <c>'nonce-…'</c> source that lets an inline <c>style</c> or
/// <c>script</c> element carrying <c>nonce="…"</c> run under a policy without <c>'unsafe-inline'</c>. Register it with
/// <see cref="CspNonceServiceCollectionExtensions.AddCspNonce" /> and name it in the policy given to
/// <see cref="AspNetCoreSecurityHeadersExtensions.UseSecurityHeaders(Microsoft.AspNetCore.Builder.IApplicationBuilder, string)" /> with
/// <see cref="AspNetCoreSecurityHeadersExtensions.NoncePlaceholder" />.
/// </summary>
/// <remarks>
/// The service is scoped, so the value belongs to one HTTP request. An interactive Blazor Server circuit has a scope of its own and therefore a
/// different value that no response policy names, and a page reached by interactive navigation has no response at all. A component in the
/// circuit that renders a nonce must use the value of the document's response: read it in the statically rendered root (<c>App</c>), pass it
/// to the interactive root component as a parameter and cascade it from there.
/// </remarks>
public interface ICspNonce
{
    /// <summary>Gets the nonce: 128 random bits in base64, created on first read and the same for the rest of the scope.</summary>
    string Value { get; }
}
