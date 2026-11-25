namespace SharedKernel.PageSections;

/// <summary>The band a page section sits on.</summary>
public enum SectionTone
{
    /// <summary>The page background.</summary>
    Plain = 0,

    /// <summary>A quiet band in the theme's alternate surface colour, such as a grey band.</summary>
    Muted = 1,

    /// <summary>A band in the theme's primary colour.</summary>
    Primary = 2,

    /// <summary>A dark band over a background image; the section needs that image.</summary>
    Image = 3,
}
