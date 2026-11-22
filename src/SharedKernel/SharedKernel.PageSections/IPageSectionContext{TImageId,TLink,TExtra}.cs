using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.PageSections;

/// <summary>
/// What the section rules ask the application: its images (whether one exists and may be shown in a culture), its link targets, and its
/// extension. ViajantesTurismo V2 checks a menu-like target and a settings block with a display condition here.
/// </summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TLink">The application's link target.</typeparam>
/// <typeparam name="TExtra">The application's extension.</typeparam>
public interface IPageSectionContext<in TImageId, TLink, TExtra> : IImageEligibility<TImageId>
    where TLink : notnull
    where TExtra : struct, IEquatable<TExtra>
{
    /// <summary>Validates a button's target, and may normalize it.</summary>
    /// <param name="target">The entered target.</param>
    /// <param name="field">The field of the button, such as <c>pt.sections[0].link</c>; put errors on its sub-fields.</param>
    /// <returns>The target to keep, or the validation errors.</returns>
    Result<TLink> ValidateLink(TLink target, string field);

    /// <summary>Validates the extension of a section.</summary>
    /// <param name="extra">The entered extension; <c>default</c> means none.</param>
    /// <param name="published">Whether the section's translation is published.</param>
    /// <param name="field">The field of the section, such as <c>pt.sections[0]</c>; put errors on its sub-fields.</param>
    /// <returns>The extension to keep, or the validation errors.</returns>
    Result<TExtra> ValidateExtra(TExtra extra, bool published, string field);

    /// <summary>Determines whether the extension shows something, so a section with only a heading and it is not empty.</summary>
    /// <param name="extra">The extension.</param>
    /// <returns><see langword="true"/> when it counts as content.</returns>
    bool CountsAsContent(TExtra extra);
}
