using System.Diagnostics.CodeAnalysis;
using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.PageSections;

/// <summary>
/// What the section rules ask the application: its images (whether one exists and may be shown in a culture), its link targets, and its
/// extension. ViajantesTurismo V2 turns its raw menu-like input into its validated target, and checks a settings block with a display
/// condition, here. An application without an extension implements only <see cref="ValidateLink"/> and the image knowledge: the extension
/// members default to accepting the value unchanged and never counting it as content.
/// </summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TLinkInput">The application's raw link target.</typeparam>
/// <typeparam name="TLink">The application's validated link target.</typeparam>
/// <typeparam name="TExtra">The application's extension.</typeparam>
public interface IPageSectionContext<in TImageId, in TLinkInput, TLink, TExtra> : IImageEligibility<TImageId>
    where TLinkInput : class
    where TLink : notnull
    where TExtra : struct, IEquatable<TExtra>
{
    /// <summary>Validates a button's raw target and turns it into the validated target.</summary>
    /// <param name="input">The entered target.</param>
    /// <param name="field">The field of the button, such as <c>pt.sections[0].link</c>; put errors on its sub-fields.</param>
    /// <returns>The validated target, or the validation errors.</returns>
    Result<TLink> ValidateLink(TLinkInput input, string field);

    /// <summary>Validates the extension of a section. The default accepts it unchanged.</summary>
    /// <param name="extra">The entered extension; <c>default</c> means none.</param>
    /// <param name="published">Whether the section's translation is published.</param>
    /// <param name="field">The field of the section, such as <c>pt.sections[0]</c>; put errors on its sub-fields.</param>
    /// <returns>The extension to keep, or the validation errors.</returns>
    [SuppressMessage("Style", "SKSTYLE009:Result-returning methods should be able to return a failure Result", Justification = "A default interface body: the contract returns failures in the implementations that override it.")]
    Result<TExtra> ValidateExtra(TExtra extra, bool published, string field) => Result.Ok(extra);

    /// <summary>
    /// Determines whether the extension shows something, so a section with only a heading and it is not empty. The default is
    /// <see langword="false"/>.
    /// </summary>
    /// <param name="extra">The extension.</param>
    /// <returns><see langword="true"/> when it counts as content.</returns>
    bool CountsAsContent(TExtra extra) => false;
}
