using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static SharedKernel.Blazor.Tests.PickerRendering;

namespace SharedKernel.Blazor.Tests;

/// <summary>
/// Tests the image picker's search: it waits for the debounce, asks for a longer query below the shortest length, and announces only
/// the latest search of an open panel, never one that a newer search or a close replaced. The debounce waits on a manual clock, so
/// whether typing falls within it never depends on how fast the test runs.
/// </summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ImagePickerSearchTests : BunitContext
{
    private static readonly TimeSpan Millisecond = TimeSpan.FromMilliseconds(1);

    private readonly PickerLibrary _library = new(40);
    private readonly ManualTimeProvider _time = new();

    /// <summary>Renders as the interactive circuit does.</summary>
    public ImagePickerSearchTests() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    [Fact]
    public async Task Typing_waits_for_the_debounce_and_searches_only_the_last_query()
    {
        // Arrange
        var debounce = TimeSpan.FromMilliseconds(100);
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Values, []),
            debounce,
            _time);
        await cut.Find($"#{Id}-find").ClickAsync(new MouseEventArgs()).Guarded();

        // Act
        var first = await cut.TypeWhileDebouncing(_time, "0", debounce);
        _time.Advance(Millisecond);
        var second = await cut.TypeWhileDebouncing(_time, "00", debounce);
        _time.Advance(Millisecond);
        var last = await cut.TypeWhileDebouncing(_time, "0012", debounce);
        await Task.WhenAll(first, second).Guarded();
        _time.Advance(debounce - Millisecond);
        string[] searchedBeforeDebounce = [.. _library.Queries];
        _time.Advance(Millisecond);
        await last.Guarded();

        // Assert
        searchedBeforeDebounce.ShouldBe([string.Empty]);
        _library.Queries.ShouldBe([string.Empty, "0012"]);
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("Showing 1 of 1 images that match ‘0012’.");
    }

    [Fact]
    public async Task A_query_below_the_shortest_length_asks_for_more_and_searches_nothing()
    {
        // Arrange
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Values, [])
            .Add(picker => picker.MinQueryLength, 3));

        // Act
        await cut.Find($"#{Id}-find").ClickAsync(new MouseEventArgs()).Guarded();
        var opened = cut.Find($"#{Id}-status").TextContent.Trim();
        await cut.Find($"#{Id}-search").InputAsync(new ChangeEventArgs { Value = "00" }).Guarded();
        var shortQuery = cut.Find($"#{Id}-panel .help").TextContent;
        await cut.Find($"#{Id}-search").InputAsync(new ChangeEventArgs { Value = "0012" }).Guarded();

        // Assert
        opened.ShouldBe("Type at least 3 characters.");
        shortQuery.ShouldBe("Type at least 3 characters.");
        _library.Queries.ShouldBe(["0012"]);
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("Showing 1 of 1 images that match ‘0012’.");
        cut.FindAll($"#{Id}-results li").Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_search_that_a_newer_one_replaced_announces_nothing_when_it_ends()
    {
        // Arrange
        var older = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var newer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _library.SearchGate = (query, _) => query switch
        {
            "0001" => older.Task,
            "0002" => newer.Task,
            _ => Task.CompletedTask,
        };
        var cut = this.RenderPicker(_library, parameters => parameters.Add(picker => picker.Catalog, PickerLibrary.Catalog()).Add(picker => picker.Values, []));
        await cut.Find($"#{Id}-find").ClickAsync(new MouseEventArgs()).Guarded();
        var replaced = cut.Find($"#{Id}-search").InputAsync(new ChangeEventArgs { Value = "0001" });
        var latest = cut.Find($"#{Id}-search").InputAsync(new ChangeEventArgs { Value = "0002" });
        newer.SetResult();
        await latest.Guarded();
        var announced = cut.Find($"#{Id}-status").TextContent;

        // Act
        older.SetResult();
        await replaced.Guarded();

        // Assert
        announced.Trim().ShouldBe("Showing 1 of 1 images that match ‘0002’.");
        cut.Find($"#{Id}-status").TextContent.ShouldBe(announced);
        cut.Find($"#{Id}-result-1").GetAttribute("aria-label").ShouldBe("Add Photo 0002");
    }

    [Fact]
    public async Task A_debouncing_search_replaced_by_a_query_below_the_shortest_length_announces_nothing_when_it_ends()
    {
        // Arrange
        var debounce = TimeSpan.FromMilliseconds(200);
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Values, [])
            .Add(picker => picker.MinQueryLength, 3),
            debounce,
            _time);
        await cut.Find($"#{Id}-find").ClickAsync(new MouseEventArgs()).Guarded();
        var debouncing = await cut.TypeWhileDebouncing(_time, "0012", debounce);
        await cut.Find($"#{Id}-search").InputAsync(new ChangeEventArgs { Value = "ab" }).Guarded();
        var announced = cut.Find($"#{Id}-status").TextContent;

        // Act
        _time.Advance(debounce);
        await debouncing.Guarded();

        // Assert
        announced.Trim().ShouldBe("Type at least 3 characters.");
        cut.Find($"#{Id}-status").TextContent.ShouldBe(announced);
        _library.Queries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Closing_the_search_while_it_starts_announces_nothing()
    {
        // Arrange
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _library.SearchGate = (_, _) => start.Task;
        var cut = this.RenderPicker(_library, parameters => parameters.Add(picker => picker.Catalog, PickerLibrary.Catalog()).Add(picker => picker.Values, []));
        var opening = cut.Find($"#{Id}-find").ClickAsync(new MouseEventArgs());
        await cut.Find($"#{Id}-find").ClickAsync(new MouseEventArgs()).Guarded();

        // Act
        start.SetResult();
        await opening.Guarded();

        // Assert
        cut.Find($"#{Id}-status").TextContent.ShouldBeEmpty();
        cut.Find($"#{Id}-find").GetAttribute("aria-expanded").ShouldBe("false");
        cut.FindAll($"#{Id}-panel").ShouldBeEmpty();
    }
}
