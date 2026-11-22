using PandaBear.Results;

namespace PandaBear.PageSections.Tests;

/// <summary>
/// A V2-shaped section context: an image library with alt text per culture, links to known pages or https addresses, a settings block
/// that needs the bank settings to publish, and conditions allowed only on the tour-information page.
/// </summary>
internal sealed class SiteContext : IPageSectionContext<Guid, NavLink, SiteExtra>
{
    private readonly Dictionary<Guid, HashSet<ContentCulture>> _images = [];

    /// <summary>Gets or sets a value indicating whether the page is the tour-information page, where conditions are allowed.</summary>
    public bool TourInformation { get; set; }

    /// <summary>Gets or sets a value indicating whether the bank settings are saved.</summary>
    public bool HasBankSettings { get; set; }

    /// <summary>Adds an image that has alt text in the given cultures.</summary>
    /// <param name="cultures">The cultures.</param>
    /// <returns>The image id.</returns>
    public Guid Add(params ContentCulture[] cultures)
    {
        var id = Guid.CreateVersion7();
        _images[id] = [.. cultures];
        return id;
    }

    /// <inheritdoc />
    public bool Exists(Guid imageId) => _images.ContainsKey(imageId);

    /// <inheritdoc />
    public bool IsEligible(Guid imageId, ContentCulture culture) => _images.TryGetValue(imageId, out var cultures) && cultures.Contains(culture);

    /// <inheritdoc />
    public Result<NavLink> ValidateLink(NavLink target, string field)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.PageKey is { } key)
        {
            return key is "about" or "contact"
                ? Result.Ok(target)
                : Result.Invalid<NavLink>(detail: "The page does not exist.", field: field + ".pageKey", message: "The page does not exist.");
        }

        var address = WebUrl.CreateHttps(target.ExternalLink, field + ".externalLink");
        return address.IsSuccess ? Result.Ok(target with { ExternalLink = address.Value.Value }) : address.ConvertError<WebUrl, NavLink>();
    }

    /// <inheritdoc />
    public Result<SiteExtra> ValidateExtra(SiteExtra extra, bool published, string field)
    {
        var errors = new ValidationErrors();
        if (!Enum.IsDefined(extra.Block))
        {
            errors.Add(Result.Invalid(detail: "The section block is not supported.", field: field + ".block", message: "The section block is not supported."));
        }
        else if (published && extra.Block == PageBlock.BankDetails && !HasBankSettings)
        {
            errors.Add(Result.Invalid(detail: "The bank details block needs the bank settings before publishing.", field: field + ".block", message: "The bank details block needs the bank settings before publishing."));
        }

        if (extra.Condition != SectionCondition.Always && !TourInformation)
        {
            errors.Add(Result.Invalid(detail: "Conditions are allowed only on the tour information page.", field: field + ".condition", message: "Conditions are allowed only on the tour information page."));
        }

        return errors.HasErrors ? errors.ToResult<SiteExtra>() : Result.Ok(extra);
    }

    /// <inheritdoc />
    public bool CountsAsContent(SiteExtra extra) => extra.Block != PageBlock.None;
}
