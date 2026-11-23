using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A numbered list of steps, such as the days of an itinerary: an <c>ol</c> (<c>site-step-list</c>) with <c>role="list"</c>, because
/// Safari drops the list semantics of a list without markers. Each step is an <c>li</c> that opens with an <c>hgroup</c> of its number,
/// formatted from its position with <see cref="NumberFormat" />, and its title heading, then its inline facts, its note and its
/// paragraphs. A step with an image puts that text beside the image in a <see cref="Split" /> that starts both columns at the top, the
/// image's side alternating from image to image when <see cref="Alternate" /> is set.
/// </summary>
public sealed partial class StepList
{
    /// <summary>Gets or sets the steps, in order; the first is number 1.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<StepItem> Steps { get; set; } = [];

    /// <summary>
    /// Gets or sets the format of a step's number, with <c>{0}</c> for its position from 1, such as "Day {0}" or "Dia {0}". It is the
    /// application's text and is formatted with the current culture.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string NumberFormat { get; set; } = string.Empty;

    /// <summary>Gets or sets the level of the step headings, from 1 to 6. The default is 3, under a section's level 2 heading.</summary>
    [Parameter]
    public int HeadingLevel { get; set; } = 3;

    /// <summary>
    /// Gets or sets the format of a step heading's id, with <c>{0}</c> for its position, such as "day-{0}", so <c>#day-2</c> links to a
    /// step. Each id must be unique on the page, so the format must hold <c>{0}</c> and give no white space.
    /// <see langword="null" />, the default, writes no id.
    /// </summary>
    [Parameter]
    public string? HeadingIdFormat { get; set; }

    /// <summary>
    /// Gets or sets whether the image side alternates from image to image, the first at the end, so the photos of an itinerary zigzag.
    /// The default is <see langword="true" />; <see langword="false" /> keeps every image at the end.
    /// </summary>
    [Parameter]
    public bool Alternate { get; set; } = true;

    /// <summary>Gets or sets how many leading images, counted in document order, load eagerly. The default is 0.</summary>
    [Parameter]
    public int EagerCount { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Steps" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="HeadingLevel" /> is not from 1 to 6, or <see cref="EagerCount" /> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// A step is invalid, <see cref="NumberFormat" /> does not format the position (positions 1 and 2 must read differently, so an
    /// escaped <c>{{0}}</c> is refused), or <see cref="HeadingIdFormat" /> does not give a unique id without white space.
    /// </exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Steps);
        Heading.RequireLevel(HeadingLevel, nameof(HeadingLevel));
        ArgumentOutOfRangeException.ThrowIfNegative(EagerCount);
        RequirePositionFormat(NumberFormat, nameof(NumberFormat), CultureInfo.CurrentCulture);
        if (HeadingIdFormat is not null)
        {
            var id = RequirePositionFormat(HeadingIdFormat, nameof(HeadingIdFormat), CultureInfo.InvariantCulture);
            if (id.Length == 0 || id.AsSpan().ContainsAnyWhiteSpace())
            {
                throw new ArgumentException("A heading id must not be empty or contain white space.", nameof(HeadingIdFormat));
            }
        }

        foreach (var step in Steps)
        {
            RequireStep(step, nameof(Steps));
        }
    }

    private string NumberText(int position) => string.Format(CultureInfo.CurrentCulture, NumberFormat, position);

    private string? HeadingId(int position) =>
        HeadingIdFormat is null ? null : string.Format(CultureInfo.InvariantCulture, HeadingIdFormat, position);

    private static string RequirePositionFormat(string format, string parameterName, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException("The format must hold {0} for the position.", parameterName);
        }

        string first;
        string second;
        try
        {
            first = string.Format(culture, format, 1);
            second = string.Format(culture, format, 2);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The format must format the position as {0} alone.", parameterName, exception);
        }

        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            throw new ArgumentException("The format must hold {0} for the position, so each step reads differently.", parameterName);
        }

        return first;
    }

    private static void RequireStep(StepItem step, string parameterName)
    {
        if (step is null)
        {
            throw new ArgumentException("A step is missing.", parameterName);
        }

        if (string.IsNullOrWhiteSpace(step.Title))
        {
            throw new ArgumentException("Every step needs a title.", parameterName);
        }

        if (step.Paragraphs is null || step.Paragraphs.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"The step '{step.Title}' has a missing paragraph list or a blank paragraph.", parameterName);
        }

        if (step.Facts is null || step.Facts.Any(fact => fact is null))
        {
            throw new ArgumentException($"The step '{step.Title}' has a missing fact list or a missing fact.", parameterName);
        }
    }
}
