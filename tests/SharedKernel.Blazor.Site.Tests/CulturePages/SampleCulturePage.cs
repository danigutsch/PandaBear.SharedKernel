using Microsoft.AspNetCore.Components;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site.Tests.CulturePages;

/// <summary>The base of the culture page tests' pages: Portuguese (default) and English, read from <see cref="CulturePageReader" />.</summary>
public abstract class SampleCulturePage : CulturePageBase<CulturePageContent>
{
    /// <summary>Gets the languages of the test site.</summary>
    public static SupportedCultures Supported { get; } =
        SupportedCultures.Create(ContentCulture.Create("pt").Value, [ContentCulture.Create("pt").Value, ContentCulture.Create("en").Value]).Value;

    /// <inheritdoc />
    protected override SupportedCultures Cultures => Supported;

    [Inject]
    private CulturePageReader Reader { get; set; } = null!;

    /// <inheritdoc />
    protected override Task<CulturePageContent> ReadContent(ContentCulture culture, CancellationToken cancellationToken) => Reader.Read(culture, cancellationToken);
}
