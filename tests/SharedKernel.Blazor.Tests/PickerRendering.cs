namespace PandaBear.Blazor.Tests;

/// <summary>Renders the image picker of the tests.</summary>
internal static class PickerRendering
{
    /// <summary>The picker's id.</summary>
    public const string Id = "photos";

    /// <summary>
    /// Renders a picker named "Photos" over the library, without debounce, with an empty catalog unless the configuration gives one.
    /// </summary>
    /// <param name="context">The bUnit context.</param>
    /// <param name="library">The library.</param>
    /// <param name="configure">More parameters.</param>
    /// <returns>The rendered picker.</returns>
    public static IRenderedComponent<ImagePicker<int, PickerImage>> RenderPicker(
        this BunitContext context,
        PickerLibrary library,
        Action<ComponentParameterCollectionBuilder<ImagePicker<int, PickerImage>>> configure) =>
        context.Render<ImagePicker<int, PickerImage>>(parameters =>
        {
            parameters
                .Add(picker => picker.Id, Id)
                .Add(picker => picker.Label, "Photos")
                .Add(picker => picker.Search, library.Search)
                .Add(picker => picker.Lookup, library.Lookup)
                .Add(picker => picker.LabelOf, image => image.Label)
                .Add(picker => picker.ThumbnailOf, image => image.Thumbnail)
                .Add(picker => picker.Debounce, TimeSpan.Zero);
            configure(parameters);
        });
}
