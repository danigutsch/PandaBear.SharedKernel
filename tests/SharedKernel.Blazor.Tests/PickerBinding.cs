using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor.Tests;

/// <summary>
/// A parent that binds a multiple-mode picker's list as <c>@bind-Values</c> does: it keeps the list the picker raised and renders the
/// picker again with it.
/// </summary>
internal sealed class PickerBinding : ComponentBase
{
    /// <summary>Gets or sets the library.</summary>
    [Parameter]
    [EditorRequired]
    public PickerLibrary Library { get; set; } = default!;

    /// <summary>Gets or sets the catalog.</summary>
    [Parameter]
    [EditorRequired]
    public PickerCatalog<int, PickerImage> Catalog { get; set; } = default!;

    /// <summary>Gets or sets the chosen images, which the picker's changes replace.</summary>
    [Parameter]
    public IReadOnlyList<int> Values { get; set; } = [];

    /// <summary>Gets or sets the picker's chosen page size.</summary>
    [Parameter]
    public int ChosenPageSize { get; set; } = ImagePicker.DefaultChosenPageSize;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenComponent<ImagePicker<int, PickerImage>>(0);
        builder.AddComponentParameter(1, nameof(ImagePicker<int, PickerImage>.Id), PickerRendering.Id);
        builder.AddComponentParameter(2, nameof(ImagePicker<int, PickerImage>.Label), "Photos");
        builder.AddComponentParameter(3, nameof(ImagePicker<int, PickerImage>.Search), (Func<string, int, CancellationToken, Task<ListSlice<PickerImage>>>)Library.Search);
        builder.AddComponentParameter(4, nameof(ImagePicker<int, PickerImage>.Lookup), (Func<IReadOnlyList<int>, CancellationToken, Task<IReadOnlyList<PickerImage>>>)Library.Lookup);
        builder.AddComponentParameter(5, nameof(ImagePicker<int, PickerImage>.Catalog), Catalog);
        builder.AddComponentParameter(6, nameof(ImagePicker<int, PickerImage>.LabelOf), (Func<PickerImage, string>)(image => image.Label));
        builder.AddComponentParameter(7, nameof(ImagePicker<int, PickerImage>.Debounce), TimeSpan.Zero);
        builder.AddComponentParameter(8, nameof(ImagePicker<int, PickerImage>.ChosenPageSize), ChosenPageSize);
        builder.AddComponentParameter(9, nameof(ImagePicker<int, PickerImage>.Reorderable), true);
        builder.AddComponentParameter(10, nameof(ImagePicker<int, PickerImage>.Values), Values);
        builder.AddComponentParameter(
            11,
            nameof(ImagePicker<int, PickerImage>.ValuesChanged),
            EventCallback.Factory.Create<IReadOnlyList<int>>(this, values => Values = values));
        builder.CloseComponent();
    }
}
