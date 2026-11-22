using PandaBear.Results;

namespace PandaBear.PageSections.Tests;

/// <summary>
/// A validated button target of the tests, opaque like V2's NavTarget: a private constructor, a Result factory from the raw input, and
/// a restore for stored input.
/// </summary>
public sealed record NavRoute
{
    private NavRoute(string? pageKey, string? externalLink)
    {
        PageKey = pageKey;
        ExternalLink = externalLink;
    }

    /// <summary>Gets the key of a page.</summary>
    public string? PageKey { get; }

    /// <summary>Gets the normalized external address.</summary>
    public string? ExternalLink { get; }

    /// <summary>Validates raw input: a known page key, or an https address.</summary>
    /// <param name="input">The input.</param>
    /// <param name="field">The field of the button.</param>
    /// <returns>The route, or validation errors.</returns>
    public static Result<NavRoute> Create(NavLink input, string field)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.PageKey is { } key)
        {
            return key is "about" or "contact"
                ? Result.Ok(new NavRoute(key, null))
                : Result.Invalid<NavRoute>(detail: "The page does not exist.", field: field + ".pageKey", message: "The page does not exist.");
        }

        var address = WebUrl.CreateHttps(input.ExternalLink, field + ".externalLink");
        return address.IsSuccess ? Result.Ok(new NavRoute(null, address.Value.Value)) : address.ConvertError<WebUrl, NavRoute>();
    }

    /// <summary>Rebuilds a stored route without validation.</summary>
    /// <param name="input">The stored input.</param>
    /// <returns>The route.</returns>
    public static NavRoute Restore(NavLink input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new NavRoute(input.PageKey, input.ExternalLink);
    }

    /// <summary>Gives the route back as raw input.</summary>
    /// <returns>The input.</returns>
    public NavLink ToInput() => new(PageKey, ExternalLink);
}
