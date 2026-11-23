namespace PandaBear.Blazor.Site;

/// <summary>One step of a <see cref="StepList" />, such as a day of an itinerary. Its number comes from its position in the list.</summary>
/// <param name="Title">The heading, such as "Valley of the Moon". It must not be blank.</param>
/// <param name="Paragraphs">The paragraphs of the step, in reading order; none may be blank.</param>
/// <param name="Note">An optional short note before the paragraphs, such as "Long drive". Null or blank renders none.</param>
/// <param name="Image">An optional image beside the text, with its caption and credit; <see langword="null" /> renders the text alone.</param>
/// <param name="Facts">The facts shown in one row under the heading, such as the distance; empty renders none.</param>
public sealed record StepItem(string Title, IReadOnlyList<string> Paragraphs, string? Note, GalleryImage? Image, IReadOnlyList<FactCard> Facts);
