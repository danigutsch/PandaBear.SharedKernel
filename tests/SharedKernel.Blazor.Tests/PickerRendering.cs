using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Tests;

/// <summary>Renders the image picker of the tests and identifies the element it focused.</summary>
internal static class PickerRendering
{
    /// <summary>The picker's id.</summary>
    public const string Id = "photos";

    /// <summary>
    /// Renders a picker named "Photos" over the library, without debounce unless one is given, with an empty catalog unless the configuration gives one.
    /// </summary>
    /// <param name="context">The bUnit context.</param>
    /// <param name="library">The library.</param>
    /// <param name="configure">More parameters.</param>
    /// <param name="debounce">The debounce, which waits on <paramref name="time"/>; none by default.</param>
    /// <param name="time">The clock of the debounce; the system clock by default.</param>
    /// <returns>The rendered picker.</returns>
    public static IRenderedComponent<ImagePicker<int, PickerImage>> RenderPicker(
        this BunitContext context,
        PickerLibrary library,
        Action<ComponentParameterCollectionBuilder<ImagePicker<int, PickerImage>>> configure,
        TimeSpan? debounce = null,
        TimeProvider? time = null) =>
        context.Render<ImagePicker<int, PickerImage>>(parameters =>
        {
            parameters
                .Add(picker => picker.Id, Id)
                .Add(picker => picker.Label, "Photos")
                .Add(picker => picker.Search, library.Search)
                .Add(picker => picker.Lookup, library.Lookup)
                .Add(picker => picker.LabelOf, image => image.Label)
                .Add(picker => picker.ThumbnailOf, image => image.Thumbnail)
                .Add(picker => picker.Debounce, debounce ?? TimeSpan.Zero)
                .Add(picker => picker.TimeProvider, time ?? TimeProvider.System);
            configure(parameters);
        });

    /// <summary>
    /// Types into the open search field and waits until that search waits for its debounce: until the clock holds the timer that fires
    /// one debounce from now, which only this search can have started, since the clock has not moved since the text was typed.
    /// </summary>
    /// <param name="cut">The picker, with the search open.</param>
    /// <param name="time">The clock of the picker's debounce.</param>
    /// <param name="text">The typed text.</param>
    /// <param name="debounce">The picker's debounce.</param>
    /// <returns>The input event, which completes when its search has finished or was replaced.</returns>
    public static async Task<Task> TypeWhileDebouncing(
        this IRenderedComponent<ImagePicker<int, PickerImage>> cut, ManualTimeProvider time, string text, TimeSpan debounce)
    {
        var due = time.GetUtcNow() + debounce;
        var typing = cut.Find($"#{Id}-search").InputAsync(new ChangeEventArgs { Value = text });
        await time.WaitForTimer(due, Xunit.TestContext.Current.CancellationToken).Guarded();
        return typing;
    }

    /// <summary>Renders a picker of two images a page inside a parent that binds its list, as <c>@bind-Values</c> does.</summary>
    /// <param name="context">The bUnit context.</param>
    /// <param name="library">The library.</param>
    /// <param name="values">The chosen images.</param>
    /// <param name="catalog">The catalog.</param>
    /// <returns>The rendered parent.</returns>
    public static IRenderedComponent<PickerBinding> RenderBinding(
        this BunitContext context,
        PickerLibrary library,
        IReadOnlyList<int> values,
        PickerCatalog<int, PickerImage> catalog) =>
        context.Render<PickerBinding>(parameters => parameters
            .Add(binding => binding.Library, library)
            .Add(binding => binding.Catalog, catalog)
            .Add(binding => binding.Values, values)
            .Add(binding => binding.ChosenPageSize, 2));

    /// <summary>
    /// Gets the id of the element reference Blazor gave an element when it created it. Read it while the element is new: bUnit's markup
    /// keeps the id only until a later render changes one of the element's attributes, although the reference itself stays valid.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The reference id.</returns>
    /// <exception cref="InvalidOperationException">The markup carries no reference id for the element.</exception>
    public static string ReferenceId(this IElement element) =>
        element.GetAttribute("blazor:elementReference") is { Length: > 0 } id ? id : throw new InvalidOperationException("The element carries no reference id.");

    /// <summary>Gets the id of the element reference focused last.</summary>
    /// <param name="interop">The bUnit JavaScript interop, which records the focus calls.</param>
    /// <returns>The reference id.</returns>
    public static string FocusedLast(this BunitJSInterop interop) =>
        ((ElementReference)interop.Invocations["Blazor._internal.domWrapper.focus"][^1].Arguments[0]!).Id;
}
