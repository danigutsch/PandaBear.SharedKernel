using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static PandaBear.Blazor.Tests.PickerRendering;

namespace PandaBear.Blazor.Tests;

/// <summary>
/// Tests the image picker when a change waits for the options of the page it shows: a second change made meanwhile is kept, a change
/// that crosses a page reads that page's options, and disposing the picker cancels what it waits for.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ImagePickerRaceTests : BunitContext
{
    private readonly PickerLibrary _library = new(40);
    private readonly TaskCompletionSource _lookup = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Renders as the interactive circuit does and lets focus calls succeed.</summary>
    public ImagePickerRaceTests()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task A_second_removal_while_the_first_waits_for_a_lookup_is_kept()
    {
        // Arrange
        _library.LookupGate = _ => _lookup.Task;
        var host = this.RenderBinding(_library, [1, 2, 3, 4], PickerLibrary.Catalog(1, 2));
        var first = host.Find($"#{Id}-remove-1").ClickAsync(new MouseEventArgs());

        // Act
        await host.Find($"#{Id}-remove-2").ClickAsync(new MouseEventArgs()).Guarded();
        _lookup.SetResult();
        await first.Guarded();

        // Assert
        host.Instance.Values.ShouldBe([3, 4]);
        host.FindAll($"#{Id} ol li span").Select(label => label.TextContent).ShouldBe(["Photo 0003", "Photo 0004"]);
    }

    [Fact]
    public async Task An_image_added_while_a_removal_waits_for_a_lookup_is_kept()
    {
        // Arrange
        _library.LookupGate = _ => _lookup.Task;
        var host = this.RenderBinding(_library, [1, 2, 3, 4], PickerLibrary.Catalog(1, 2));
        var removal = host.Find($"#{Id}-remove-1").ClickAsync(new MouseEventArgs());
        await host.Find($"#{Id}-find").ClickAsync(new MouseEventArgs()).Guarded();

        // Act
        await host.Find($"#{Id}-result-5").ClickAsync(new MouseEventArgs()).Guarded();
        _lookup.SetResult();
        await removal.Guarded();

        // Assert
        host.Instance.Values.ShouldBe([2, 3, 4, 5]);
    }

    [Fact]
    public async Task A_removal_while_a_move_waits_for_a_lookup_is_kept_and_the_move_applies_to_the_list_that_remains()
    {
        // Arrange
        _library.LookupGate = _ => _lookup.Task;
        var host = this.RenderBinding(_library, [1, 2, 3, 4], PickerLibrary.Catalog(1, 2, 3));
        var move = host.Find($"#{Id}-later-2").ClickAsync(new MouseEventArgs());

        // Act
        await host.Find($"#{Id}-remove-1").ClickAsync(new MouseEventArgs()).Guarded();
        _lookup.SetResult();
        await move.Guarded();

        // Assert
        host.Instance.Values.ShouldBe([3, 2, 4]);
        host.FindAll($"#{Id} ol li span").Select(label => label.TextContent).ShouldBe(["Photo 0003", "Photo 0002"]);
    }

    [Fact]
    public async Task A_move_onto_another_page_reads_its_options_shows_it_and_keeps_focus_on_the_moved_image()
    {
        // Arrange
        IReadOnlyList<int> values = [1, 2, 3, 4];
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(1, 2))
            .Add(picker => picker.Values, values)
            .Add(picker => picker.ValuesChanged, changed => values = changed)
            .Add(picker => picker.ChosenPageSize, 2)
            .Add(picker => picker.Reorderable, true));
        var moved = cut.Find($"#{Id}-later-2").ReferenceId();

        // Act
        await cut.Find($"#{Id}-later-2").ClickAsync(new MouseEventArgs()).Guarded();

        // Assert
        cut.Find($"#{Id}-later-3").GetAttribute("aria-label").ShouldBe("Move Photo 0002 later");
        values.ShouldBe([1, 3, 2, 4]);
        _library.Lookups.ShouldBe([[4]]);
        cut.Find($"#{Id} ol").GetAttribute("start").ShouldBe("3");
        cut.FindAll($"#{Id} ol li span").Select(label => label.TextContent).ShouldBe(["Photo 0002", "Photo 0004"]);
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("Photo 0002 moved to position 3 of 4.");
        JSInterop.FocusedLast().ShouldBe(moved);
    }

    [Fact]
    public async Task Disposing_the_picker_cancels_a_lookup_it_waits_for()
    {
        // Arrange
        _library.LookupGate = cancellationToken => Task.Delay(Timeout.Infinite, cancellationToken);
        this.RenderPicker(_library, parameters => parameters.Add(picker => picker.Catalog, PickerLibrary.Catalog()).Add(picker => picker.Value, 7));
        var waiting = _library.LookupToken.IsCancellationRequested;

        // Act
        await DisposeComponentsAsync().Guarded();

        // Assert
        waiting.ShouldBeFalse();
        _library.LookupToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Disposing_the_picker_cancels_a_search_it_waits_for()
    {
        // Arrange
        _library.SearchGate = (_, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
        var cut = this.RenderPicker(_library, parameters => parameters.Add(picker => picker.Catalog, PickerLibrary.Catalog()).Add(picker => picker.Values, []));
        _ = cut.Find($"#{Id}-find").ClickAsync(new MouseEventArgs());
        var waiting = _library.SearchToken.IsCancellationRequested;

        // Act
        await DisposeComponentsAsync().Guarded();

        // Assert
        waiting.ShouldBeFalse();
        _library.Queries.ShouldBe([string.Empty]);
        _library.SearchToken.IsCancellationRequested.ShouldBeTrue();
    }
}
