using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using SharedKernel.ContentRights;

namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>A localized compact credit and its corresponding standalone page entry.</summary>
public sealed class PhotoCreditsSample : ComponentBase
{
    private static readonly PhotoCreditLabels Labels = new(
        "Fotógrafo", "Titular", "Licenciante", "Crédito adicional", "Fonte original",
        "Termos", "Crédito completo", "Alterações", "Alterações anteriores");

    /// <summary>Gets or sets whether to render the compact credit in the shared thumbnail gallery.</summary>
    [Parameter]
    public bool UseGallery { get; set; }

    /// <summary>Gets or sets whether to expand the credit inline instead of linking to its standalone entry.</summary>
    [Parameter]
    public bool InlineDetails { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var license = ContentLicense.Create(ContentLicenseKind.CcBy, "Creative Commons Atribuição 4.0",
            "https://creativecommons.org/licenses/by/4.0/legalcode.en").ShouldBeSuccess();
        var credit = ContentAttribution.Create(new ContentAttributionDefinition
        {
            Parties = [new ContentCreditParty("AssociaçãoDeFotografiaDeMontanha".PadRight(170, 'a'), ContentCreditRole.Creator)],
            OriginalSourceUrl = "https://archive.example/serra",
            ModificationNotice = "Cores ajustadas",
            License = license,
        }).ShouldBeSuccess().ToPublicCredit();
        var item = new PhotoCreditItem("serra", "Serra Azul", credit)
        {
            TermsLabel = "CC BY 4.0",
            DetailsHref = InlineDetails ? null : LinkTarget.FromString("#serra"),
        };

        builder.OpenElement(0, "main");
        if (UseGallery)
        {
            var image = ResponsiveImage.Create(
                LinkTarget.FromString(BandsSample.WhitePhoto), 1600, 900, ImageAlternative.Describe("Vista da Serra Azul"));
            builder.OpenComponent<ImageGrid>(10);
            builder.AddComponentParameter(11, nameof(ImageGrid.Images), new[] { new GalleryImage(image, "Vale ao amanhecer") { ReviewedCredit = item } });
            builder.AddComponentParameter(12, nameof(ImageGrid.Layout), ImageGridLayout.Thumbnails);
            builder.AddComponentParameter(13, nameof(ImageGrid.CreditLabels), Labels);
            builder.CloseComponent();
        }
        else
        {
            builder.OpenComponent<PhotoCredit>(1);
            builder.AddComponentParameter(2, nameof(PhotoCredit.Item), item);
            builder.AddComponentParameter(3, nameof(PhotoCredit.Labels), Labels);
            builder.AddComponentParameter(4, nameof(PhotoCredit.Compact), true);
            builder.CloseComponent();
        }
        builder.OpenComponent<PhotoCreditsPage>(5);
        builder.AddComponentParameter(6, nameof(PhotoCreditsPage.Heading), "Créditos");
        builder.AddComponentParameter(7, nameof(PhotoCreditsPage.Intro), "Créditos para as fotos da Serra Azul.");
        builder.AddComponentParameter(8, nameof(PhotoCreditsPage.Items), new[] { item });
        builder.AddComponentParameter(9, nameof(PhotoCreditsPage.Labels), Labels);
        builder.CloseComponent();
        builder.CloseElement();
    }
}
