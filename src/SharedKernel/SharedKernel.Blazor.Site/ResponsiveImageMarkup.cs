using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>Renders a <see cref="ResponsiveImage" /> as an <c>img</c>, or as a <c>picture</c> when it has sources.</summary>
internal static class ResponsiveImageMarkup
{
    /// <summary>Creates the image fragment.</summary>
    /// <param name="image">The image.</param>
    /// <param name="eager">Whether the image loads eagerly instead of lazily.</param>
    /// <param name="highPriority">Whether the image is fetched with high priority, for the largest image above the fold.</param>
    /// <param name="sizes">The <c>sizes</c> written instead of the image's own where width descriptors need one, or <see langword="null" />.</param>
    /// <param name="language">The optional language of the image's alternative text.</param>
    /// <returns>The fragment.</returns>
    public static RenderFragment Render(ResponsiveImage image, bool eager, bool highPriority, string? sizes = null, string? language = null) => builder =>
    {
        var effectiveSizes = sizes ?? image.Sizes;
        if (image.UsesPicture)
        {
            builder.OpenElement(0, "picture");
            foreach (var source in image.Sources)
            {
                builder.OpenElement(1, "source");
                builder.AddAttribute(2, "type", source.Type);
                builder.AddAttribute(3, "srcset", source.SrcSet);
                builder.AddAttribute(4, "sizes", source.UsesWidthDescriptors ? effectiveSizes : null);
                builder.CloseElement();
            }
        }

        var hasSrcSet = image.SrcSet.Count > 0;
        builder.OpenElement(5, "img");
        builder.AddAttribute(6, "src", image.Src.Value);
        builder.AddAttribute(7, "srcset", hasSrcSet ? string.Join(", ", image.SrcSet) : null);
        builder.AddAttribute(8, "sizes", hasSrcSet && image.SrcSet[0].Width is not null ? effectiveSizes : null);
        builder.AddAttribute(9, "alt", image.Alt.Text);
        builder.AddAttribute(10, "width", image.Width);
        builder.AddAttribute(11, "height", image.Height);
        builder.AddAttribute(12, "loading", eager ? "eager" : "lazy");
        builder.AddAttribute(13, "fetchpriority", highPriority ? "high" : null);
        builder.AddAttribute(14, "lang", language);
        builder.CloseElement();

        if (image.UsesPicture)
        {
            builder.CloseElement();
        }
    };
}
