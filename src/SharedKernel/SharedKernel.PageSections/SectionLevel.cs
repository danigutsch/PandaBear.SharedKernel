namespace PandaBear.PageSections;

/// <summary>The level of a page section's heading under the page title.</summary>
public enum SectionLevel
{
    /// <summary>A section of the page: a level-two heading.</summary>
    Section = 0,

    /// <summary>A part of the section before it: a level-three heading; it needs a heading and a section with one before it.</summary>
    Subsection = 1,
}
