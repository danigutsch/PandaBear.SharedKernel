namespace SharedKernel.PageSections;

/// <summary>How a page section lays out its images.</summary>
public enum SectionImageLayout
{
    /// <summary>A collage of the images below the rest of the section.</summary>
    Mosaic = 0,

    /// <summary>The images in a column beside the rest of the section.</summary>
    BesideText = 1,
}
