namespace PandaBear.Blazor.Site;

/// <summary>How an <see cref="ImageGrid" /> arranges its images.</summary>
public enum ImageGridLayout
{
    /// <summary>
    /// Rows of equal columns, at least 14rem wide, each image at its own aspect ratio and at most 40rem or 80% of the viewport tall.
    /// </summary>
    Grid = 0,

    /// <summary>
    /// A collage of mixed aspect ratios: columns at least 14rem wide that the images fill from top to bottom, one column after the
    /// other in document order, each image at its own aspect ratio, never cropped.
    /// </summary>
    Mosaic = 1,

    /// <summary>
    /// Thumbnails: every image cropped to the one aspect ratio of <c>--site-thumbnail-aspect-ratio</c> (square by default), in rows of
    /// equal columns at least <c>--site-thumbnail-min-width</c> (6.5rem) wide and at most six to a row, so a phone shows two or three
    /// to a row and a page of many photos stays short.
    /// </summary>
    Thumbnails = 2,
}
