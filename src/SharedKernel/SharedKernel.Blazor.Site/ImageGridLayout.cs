namespace PandaBear.Blazor.Site;

/// <summary>How an <see cref="ImageGrid" /> arranges its images.</summary>
public enum ImageGridLayout
{
    /// <summary>Rows of equal columns, at least 14rem wide.</summary>
    Grid = 0,

    /// <summary>
    /// A collage of mixed aspect ratios: columns at least 14rem wide that the images fill from top to bottom, one column after the
    /// other in document order, each image at its own aspect ratio, never cropped.
    /// </summary>
    Mosaic = 1,
}
