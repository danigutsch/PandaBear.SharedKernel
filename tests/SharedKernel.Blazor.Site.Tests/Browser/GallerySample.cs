using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// A page for the image grid tests: a section whose body, or whose media beside its text, is an image grid of a few photos, and a
/// band whose text and photos are side by side without a heading.
/// </summary>
public sealed class GallerySample : ComponentBase
{
    /// <summary>The address of an all-white portrait photo, 9 by 16, that <see cref="LayoutPage.Serve{TComponent}" /> serves.</summary>
    public const string TallPhoto = "/tall.svg";

    /// <summary>Gets or sets the number of photos.</summary>
    [Parameter]
    public int Count { get; set; } = 1;

    /// <summary>Gets or sets the layout of the grid.</summary>
    [Parameter]
    public ImageGridLayout Layout { get; set; }

    /// <summary>Gets or sets a value indicating whether the grid is the section's media instead of its body.</summary>
    [Parameter]
    public bool InMedia { get; set; }

    /// <summary>Gets or sets a value indicating whether the photos are portrait.</summary>
    [Parameter]
    public bool Tall { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "main");
        builder.OpenComponent<Section>(1);
        builder.AddComponentParameter(2, nameof(Section.Title), "Gallery");
        if (InMedia)
        {
            builder.AddComponentParameter(3, nameof(Section.ChildContent), (RenderFragment)(text => text.AddMarkupContent(0, "<p>Where we have been.</p>")));
            builder.AddComponentParameter(4, nameof(Section.Media), (RenderFragment)Grid);
        }
        else
        {
            builder.AddComponentParameter(5, nameof(Section.ChildContent), (RenderFragment)Grid);
        }

        builder.CloseComponent();
        builder.OpenComponent<Band>(6);
        builder.AddAttribute(7, "id", "headless");
        builder.AddComponentParameter(8, nameof(Band.Tone), BandTone.Muted);
        builder.AddComponentParameter(9, nameof(Band.ChildContent), (RenderFragment)(band =>
        {
            band.OpenComponent<Split>(0);
            band.AddComponentParameter(1, nameof(Split.ChildContent), (RenderFragment)(text => text.AddMarkupContent(0, "<p>Travel with feeling.</p>")));
            band.AddComponentParameter(2, nameof(Split.Media), (RenderFragment)Grid);
            band.CloseComponent();
        }));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private void Grid(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ImageGrid>(0);
        builder.AddComponentParameter(1, nameof(ImageGrid.Layout), Layout);
        builder.AddComponentParameter(2, nameof(ImageGrid.EagerCount), Count);
        builder.AddComponentParameter(3, nameof(ImageGrid.Images), Enumerable.Range(0, Count)
            .Select(_ => new GalleryImage(Tall
                ? ResponsiveImage.Create(LinkTarget.FromString(TallPhoto), 900, 1600, ImageAlternative.Decorative())
                : ResponsiveImage.Create(LinkTarget.FromString(BandsSample.WhitePhoto), 1600, 900, ImageAlternative.Decorative())))
            .ToArray());
        builder.CloseComponent();
    }
}
