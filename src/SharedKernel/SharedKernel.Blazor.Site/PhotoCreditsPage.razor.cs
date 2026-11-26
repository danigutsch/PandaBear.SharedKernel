using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>A caller-routed credits section with addressable photo entries and a coverage introduction.</summary>
public sealed partial class PhotoCreditsPage
{
    /// <summary>The maximum number of credits allowed in a single rendered page.</summary>
    public const int MaxItems = 100;

    /// <summary>Gets or sets the localized section heading.</summary>
    [Parameter]
    [EditorRequired]
    public string Heading { get; set; } = string.Empty;

    /// <summary>Gets or sets the localized introduction explaining which assets and terms the page covers.</summary>
    [Parameter]
    public string? Intro { get; set; }

    /// <summary>Gets or sets the optional unique id for the section heading.</summary>
    [Parameter]
    public string? HeadingId { get; set; }

    /// <summary>Gets or sets the section heading level, from 1 to 5. Individual entries use the next level.</summary>
    [Parameter]
    public int Level { get; set; } = 1;

    /// <summary>Gets or sets the caller's bounded list of approved credits in display order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<PhotoCreditItem> Items { get; set; } = [];

    /// <summary>Gets or sets the localized labels used in each credit.</summary>
    [Parameter]
    [EditorRequired]
    public PhotoCreditLabels Labels { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Items);
        ArgumentNullException.ThrowIfNull(Labels);
        Labels.Require(nameof(Labels));

        if (Level is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "The credits heading level must be from 1 to 5.");
        }

        if (Items.Count > MaxItems)
        {
            throw new ArgumentOutOfRangeException(nameof(Items), Items.Count, "The credit list exceeds the page limit.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (HeadingId is not null)
        {
            ids.Add(HeadingId);
        }

        foreach (var item in Items)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(Items));
            item.Require(nameof(Items));
            if (!ids.Add(item.Id))
            {
                throw new ArgumentException("Photo credit ids must be unique on the page.", nameof(Items));
            }
        }
    }
}
