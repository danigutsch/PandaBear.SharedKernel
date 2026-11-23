using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A plain list of lines of text, such as what a tour includes: a <c>ul</c>, or an <c>ol</c> when <see cref="Ordered" />, with the
/// class <c>site-text-list</c> and one <c>li</c> per line, keeping the browser's bullets or numbers. An empty list renders nothing.
/// </summary>
public sealed partial class TextList
{
    /// <summary>Gets or sets the lines, in reading order. None may be blank.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<string> Items { get; set; } = [];

    /// <summary>Gets or sets whether the order of the lines matters, which makes the list an <c>ol</c>. The default is <see langword="false" />.</summary>
    [Parameter]
    public bool Ordered { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Items" /> is null.</exception>
    /// <exception cref="ArgumentException">A line is null or blank.</exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Items);
        if (Items.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A line of the list is missing or blank.", nameof(Items));
        }
    }
}
