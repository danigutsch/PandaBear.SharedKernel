namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// One RFC 8693 token-exchange target: the requested audience and the optional scope.
/// </summary>
/// <param name="Audience">The <c>audience</c> parameter.</param>
/// <param name="Scope">The <c>scope</c> parameter, or <see langword="null" /> to omit it.</param>
internal sealed record TokenExchangeTarget(string Audience, string? Scope);
