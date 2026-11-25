namespace SharedKernel.Blazor.Site.Theming;

/// <summary>The header of a <see cref="ThemeDocument" />: the height of the logo in the header's brand slot.</summary>
/// <param name="BrandHeight">The logo height in rem (<c>header.brandHeight</c>); <see langword="null" /> keeps <see cref="SiteTheme.DefaultBrandHeight" />.</param>
public sealed record ThemeHeaderDocument(decimal? BrandHeight);
