namespace PandaBear.Blazor.Site;

/// <summary>One image of an <see cref="ImageGrid" />.</summary>
/// <param name="Image">The image, with its width, height, accessibility decision and optional responsive sources.</param>
/// <param name="Caption">The optional visible caption.</param>
public sealed record GalleryImage(ResponsiveImage Image, string? Caption = null);
