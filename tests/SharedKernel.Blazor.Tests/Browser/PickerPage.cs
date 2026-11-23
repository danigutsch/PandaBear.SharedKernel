using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor.Tests.Browser;

/// <summary>
/// An edit page as an application writes one: a reorderable picker of photos, the first <see cref="Chosen"/> images of the library, and
/// a single picker of a cover, image 1. It persists only the options of the first chosen page, which it reads once while prerendering.
/// </summary>
internal sealed class PickerPage : ComponentBase
{
    private IReadOnlyList<int> _photos = [];
    private int? _cover = 1;
    private PickerCatalog<int, PickerImage> _catalog = default!;

    /// <summary>Gets or sets the library.</summary>
    [Inject]
    public PickerLibrary Library { get; set; } = default!;

    /// <summary>Gets or sets how many of the first images of the library are chosen as photos.</summary>
    [Parameter]
    public int Chosen { get; set; }

    /// <summary>Gets or sets the options persisted from the prerender to the interactive render: those of the first chosen page.</summary>
    [PersistentState]
    public List<PickerImage>? Known { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _photos = [.. Enumerable.Range(1, Chosen)];
        Known ??= [.. await Library.Lookup(ImagePicker.FirstChosenPage(_photos.Append(1)), CancellationToken.None)];
        _catalog = new PickerCatalog<int, PickerImage>(image => image.Id, Known);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenComponent<InteractiveMarker>(0);
        builder.CloseComponent();
        builder.OpenComponent<ImagePicker<int, PickerImage>>(1);
        AddLibrary(builder, "photos", "Photos");
        builder.AddComponentParameter(10, nameof(ImagePicker<int, PickerImage>.Values), _photos);
        builder.AddComponentParameter(
            11,
            nameof(ImagePicker<int, PickerImage>.ValuesChanged),
            EventCallback.Factory.Create<IReadOnlyList<int>>(this, photos => _photos = photos));
        builder.AddComponentParameter(12, nameof(ImagePicker<int, PickerImage>.Reorderable), true);
        builder.CloseComponent();
        builder.OpenComponent<ImagePicker<int, PickerImage>>(13);
        AddLibrary(builder, "cover", "Cover");
        builder.AddComponentParameter(20, nameof(ImagePicker<int, PickerImage>.Value), _cover);
        builder.AddComponentParameter(21, nameof(ImagePicker<int, PickerImage>.ValueChanged), EventCallback.Factory.Create<int?>(this, cover => _cover = cover));
        builder.CloseComponent();
    }

    private void AddLibrary(RenderTreeBuilder builder, string id, string label)
    {
        builder.AddComponentParameter(2, nameof(ImagePicker<int, PickerImage>.Id), id);
        builder.AddComponentParameter(3, nameof(ImagePicker<int, PickerImage>.Label), label);
        builder.AddComponentParameter(4, nameof(ImagePicker<int, PickerImage>.Search), (Func<string, int, CancellationToken, Task<ListSlice<PickerImage>>>)Library.Search);
        builder.AddComponentParameter(5, nameof(ImagePicker<int, PickerImage>.Lookup), (Func<IReadOnlyList<int>, CancellationToken, Task<IReadOnlyList<PickerImage>>>)Library.Lookup);
        builder.AddComponentParameter(6, nameof(ImagePicker<int, PickerImage>.Catalog), _catalog);
        builder.AddComponentParameter(7, nameof(ImagePicker<int, PickerImage>.LabelOf), (Func<PickerImage, string>)(image => image.Label));
        builder.AddComponentParameter(8, nameof(ImagePicker<int, PickerImage>.ThumbnailOf), (Func<PickerImage, string?>)(image => image.Thumbnail));
        builder.AddComponentParameter(9, nameof(ImagePicker<int, PickerImage>.Debounce), TimeSpan.Zero);
    }
}
