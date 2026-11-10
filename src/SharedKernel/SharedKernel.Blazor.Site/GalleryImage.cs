namespace PandaBear.Blazor.Site;

/// <summary>One image of an <see cref="ImageGrid" />.</summary>
/// <param name="Src">The image address, root-relative or https.</param>
/// <param name="Alt">The accessibility decision: a description, or decorative.</param>
/// <param name="Width">The intrinsic width in pixels, which reserves the space and avoids layout shift.</param>
/// <param name="Height">The intrinsic height in pixels.</param>
/// <param name="Caption">The optional visible caption.</param>
public sealed record GalleryImage(LinkTarget Src, ImageAlternative Alt, int Width, int Height, string? Caption = null);
