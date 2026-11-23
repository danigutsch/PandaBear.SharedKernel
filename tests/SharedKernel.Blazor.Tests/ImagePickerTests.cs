using Microsoft.AspNetCore.Components;
using static PandaBear.Blazor.Tests.PickerRendering;

namespace PandaBear.Blazor.Tests;

/// <summary>
/// Tests the image picker: it shows the chosen images from the page's catalog a page at a time, searches the library only after the
/// find button on the interactive page, at most a bounded number of options at a time, announces in one status region, and keeps focus
/// usable.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ImagePickerTests : BunitContext
{
    private readonly PickerLibrary _library = new(40);

    /// <summary>Renders as the interactive circuit does and lets focus calls succeed.</summary>
    public ImagePickerTests()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void A_prerendered_picker_shows_the_chosen_image_disables_its_controls_and_searches_nothing()
    {
        // Arrange
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        // Act
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(2))
            .Add(picker => picker.Value, 2));

        // Assert
        var remove = cut.Find($"#{Id}-remove-1");
        remove.GetAttribute("aria-label").ShouldBe("Remove Photo 0002");
        remove.HasAttribute("disabled").ShouldBeTrue();
        var find = cut.Find($"#{Id}-find");
        find.HasAttribute("disabled").ShouldBeTrue();
        find.GetAttribute("aria-expanded").ShouldBe("false");
        find.GetAttribute("aria-controls").ShouldBe($"{Id}-panel");
        find.GetAttribute("aria-label").ShouldBe("Find an image, Photos");
        cut.FindAll($"#{Id}-panel").ShouldBeEmpty();
        cut.Find($"#{Id}-status").TextContent.ShouldBeEmpty();
        _library.Queries.ShouldBeEmpty();
        _library.Lookups.ShouldBeEmpty();
    }

    [Fact]
    public void The_picker_is_a_group_named_by_its_legend_described_by_its_help_and_messages_with_one_atomic_polite_status()
    {
        // Arrange & Act
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Values, [])
            .Add(picker => picker.Help, "Up to 60 photos.")
            .Add(picker => picker.Messages, ["Choose at least one photo.", "Every photo needs alt text."]));

        // Assert
        var group = cut.Find($"fieldset#{Id}");
        group.QuerySelector("legend")!.TextContent.ShouldBe("Photos");
        group.GetAttribute("aria-describedby").ShouldBe($"{Id}-help {Id}-error {Id}-error-2");
        cut.Find($"#{Id}-help").TextContent.ShouldBe("Up to 60 photos.");
        cut.Find($"#{Id}-error").TextContent.ShouldBe("Choose at least one photo.");
        cut.Find($"#{Id}-error-2").TextContent.ShouldBe("Every photo needs alt text.");
        cut.FindAll("[role='status']").Count.ShouldBe(1);
        var status = cut.Find($"#{Id}-status");
        status.GetAttribute("role").ShouldBe("status");
        status.GetAttribute("aria-atomic").ShouldBe("true");
        cut.Find(".image-picker__empty").TextContent.ShouldBe("No image chosen.");
    }

    [Fact]
    public async Task Opening_the_search_lists_the_first_options_and_typing_narrows_them_with_an_announcement()
    {
        // Arrange
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Values, [])
            .Add(picker => picker.SearchLimit, 5));
        await cut.Find($"#{Id}-find").ClickAsync();
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("Showing the newest 5 of 40 images.");
        cut.Find($"#{Id}-find").GetAttribute("aria-expanded").ShouldBe("true");
        cut.Find($"#{Id}-find").TextContent.ShouldBe("Close the image search");
        cut.FindAll($"#{Id}-results li").Count.ShouldBe(5);
        cut.Find($"#{Id}-panel .help").TextContent.ShouldBe("Only the first 5 are shown. Type more to find the others.");

        // Act
        await cut.Find($"#{Id}-search").InputAsync(new ChangeEventArgs { Value = "PHOTO 0012" });

        // Assert
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("Showing 1 of 1 images that match ‘PHOTO 0012’.");
        cut.FindAll($"#{Id}-results li").Count.ShouldBe(1);
        var add = cut.Find($"#{Id}-result-1");
        add.GetAttribute("aria-label").ShouldBe("Add Photo 0012");
        add.TextContent.ShouldBe("Add");
        add.HasAttribute("aria-disabled").ShouldBeFalse();
        _library.Queries.ShouldBe([string.Empty, "PHOTO 0012"]);
    }

    [Fact]
    public async Task A_search_that_matches_nothing_says_so_and_an_empty_library_shows_the_application_content()
    {
        // Arrange
        var empty = new PickerLibrary(0);
        var cut = this.RenderPicker(empty, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Values, [])
            .Add(picker => picker.EmptyLibrary, "<a id=\"upload\" href=\"/images/new\">Upload an image</a>"));

        // Act
        await cut.Find($"#{Id}-find").ClickAsync();

        // Assert
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("There are no images in the library yet.");
        cut.Find($"#{Id}-panel #upload").TextContent.ShouldBe("Upload an image");
        await cut.Find($"#{Id}-search").InputAsync(new ChangeEventArgs { Value = "lake" });
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("No images match ‘lake’.");
        cut.FindAll($"#{Id}-panel #upload").ShouldBeEmpty();
    }

    [Fact]
    public async Task Adding_raises_a_new_list_with_the_image_appended_and_shows_it_as_chosen_in_the_results()
    {
        // Arrange
        IReadOnlyList<int> values = [2];
        var original = values;
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(2))
            .Add(picker => picker.Values, values)
            .Add(picker => picker.ValuesChanged, changed => values = changed));
        await cut.Find($"#{Id}-find").ClickAsync();
        await cut.Find($"#{Id}-search").InputAsync(new ChangeEventArgs { Value = "0007" });

        // Act
        await cut.Find($"#{Id}-result-1").ClickAsync();
        cut.Render(parameters => parameters.Add(picker => picker.Values, values));

        // Assert
        values.ShouldBe([2, 7]);
        original.ShouldBe([2]);
        cut.FindAll($"#{Id} ol li span").Select(label => label.TextContent).ShouldBe(["Photo 0002", "Photo 0007"]);
        var added = cut.Find($"#{Id}-result-1");
        added.TextContent.ShouldBe("Chosen");
        added.GetAttribute("aria-label").ShouldBe("Photo 0007, chosen");
        added.GetAttribute("aria-disabled").ShouldBe("true");
        cut.Find($"#{Id}-find").GetAttribute("aria-expanded").ShouldBe("true");
        _library.Lookups.ShouldBeEmpty();
    }

    [Fact]
    public async Task Choosing_one_image_reports_it_closes_the_search_and_focuses_the_find_button()
    {
        // Arrange
        int? chosen = null;
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.ValueChanged, value => chosen = value));
        await cut.Find($"#{Id}-find").ClickAsync();
        cut.Find($"#{Id}-result-3").GetAttribute("aria-label").ShouldBe("Choose Photo 0003");

        // Act
        await cut.Find($"#{Id}-result-3").ClickAsync();

        // Assert
        chosen.ShouldBe(3);
        cut.Find($"#{Id}-find").GetAttribute("aria-expanded").ShouldBe("false");
        cut.FindAll($"#{Id}-panel").ShouldBeEmpty();
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task Removing_an_image_keeps_the_others_in_order_and_focuses_the_nearest_remove_button()
    {
        // Arrange
        IReadOnlyList<int> values = [1, 2, 3];
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(1, 2, 3))
            .Add(picker => picker.Values, values)
            .Add(picker => picker.ValuesChanged, changed => values = changed));

        // Act
        await cut.Find($"#{Id}-remove-2").ClickAsync();
        cut.Render(parameters => parameters.Add(picker => picker.Values, values));

        // Assert
        values.ShouldBe([1, 3]);
        cut.FindAll($"#{Id} ol li span").Select(label => label.TextContent).ShouldBe(["Photo 0001", "Photo 0003"]);
        JSInterop.VerifyFocusAsyncInvoke();
        cut.Find($"#{Id}-remove-2").GetAttribute("aria-label").ShouldBe("Remove Photo 0003");
    }

    [Fact]
    public async Task Removing_the_last_image_focuses_the_find_button()
    {
        // Arrange
        IReadOnlyList<int> values = [1];
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(1))
            .Add(picker => picker.Values, values)
            .Add(picker => picker.ValuesChanged, changed => values = changed));

        // Act
        await cut.Find($"#{Id}-remove-1").ClickAsync();
        cut.Render(parameters => parameters.Add(picker => picker.Values, values));

        // Assert
        values.ShouldBeEmpty();
        cut.Find(".image-picker__empty").TextContent.ShouldBe("No image chosen.");
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task Removing_the_single_image_reports_none_and_focuses_the_find_button()
    {
        // Arrange
        int? value = 4;
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(4))
            .Add(picker => picker.Value, value)
            .Add(picker => picker.ValueChanged, changed => value = changed));

        // Act
        await cut.Find($"#{Id}-remove-1").ClickAsync();

        // Assert
        value.ShouldBeNull();
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task A_long_chosen_list_is_shown_a_page_at_a_time_reading_only_the_options_of_the_page_shown()
    {
        // Arrange
        IReadOnlyList<int> values = [.. Enumerable.Range(1, 30)];
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(ImagePicker.FirstChosenPage(values)))
            .Add(picker => picker.Values, values));
        cut.FindAll($"#{Id} ol li").Count.ShouldBe(ImagePicker.DefaultChosenPageSize);
        cut.Find($"#{Id} ol").HasAttribute("start").ShouldBeFalse();
        cut.Find($"#{Id}-range").TextContent.ShouldBe("Showing 1 to 24 of 30 chosen images.");
        cut.Find($"#{Id}-range").HasAttribute("role").ShouldBeFalse();
        cut.Find($"#{Id}-previous").GetAttribute("aria-disabled").ShouldBe("true");
        cut.Find($"#{Id}-previous").GetAttribute("aria-label").ShouldBe("Previous images, Photos");
        _library.Lookups.ShouldBeEmpty();

        // Act
        await cut.Find($"#{Id}-next").ClickAsync();

        // Assert
        cut.Find($"#{Id}-range").TextContent.ShouldBe("Showing 25 to 30 of 30 chosen images.");
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("Showing 25 to 30 of 30 chosen images.");
        cut.FindAll($"#{Id} ol li span").Select(label => label.TextContent).ShouldBe(["Photo 0025", "Photo 0026", "Photo 0027", "Photo 0028", "Photo 0029", "Photo 0030"]);
        cut.Find($"#{Id} ol").GetAttribute("start").ShouldBe("25");
        cut.Find($"#{Id}-remove-25").GetAttribute("aria-label").ShouldBe("Remove Photo 0025");
        cut.Find($"#{Id}-next").GetAttribute("aria-disabled").ShouldBe("true");
        cut.Find($"#{Id}-previous").HasAttribute("aria-disabled").ShouldBeFalse();
        _library.Lookups.ShouldBe([[25, 26, 27, 28, 29, 30]]);
    }

    [Fact]
    public async Task Removing_the_only_image_of_the_last_page_shows_the_page_before_with_focus_on_its_last_image()
    {
        // Arrange
        IReadOnlyList<int> values = [.. Enumerable.Range(1, ImagePicker.DefaultChosenPageSize + 1)];
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Values, values)
            .Add(picker => picker.ValuesChanged, changed => values = changed));
        await cut.Find($"#{Id}-next").ClickAsync();
        cut.FindAll($"#{Id} ol li").Count.ShouldBe(1);

        // Act
        await cut.Find($"#{Id}-remove-25").ClickAsync();
        cut.Render(parameters => parameters.Add(picker => picker.Values, values));

        // Assert
        values.Count.ShouldBe(ImagePicker.DefaultChosenPageSize);
        cut.FindAll($"#{Id} ol li").Count.ShouldBe(ImagePicker.DefaultChosenPageSize);
        cut.FindAll($"#{Id}-range").ShouldBeEmpty();
        cut.Find($"#{Id} ol li:last-child span").TextContent.ShouldBe("Photo 0024");
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void A_chosen_image_that_left_the_library_is_named_as_such()
    {
        // Arrange & Act
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Value, 99));

        // Assert
        cut.Find($"#{Id}-remove-1").GetAttribute("aria-label").ShouldBe("Remove An image that is no longer in the library");
        cut.FindAll($"#{Id} ol img").ShouldBeEmpty();
        _library.Lookups.ShouldBe([[99]]);
    }

    [Fact]
    public async Task A_full_list_refuses_another_image_and_says_why()
    {
        // Arrange
        IReadOnlyList<int> values = [1, 2];
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(1, 2))
            .Add(picker => picker.Values, values)
            .Add(picker => picker.ValuesChanged, changed => values = changed)
            .Add(picker => picker.MaxCount, 2));
        await cut.Find($"#{Id}-find").ClickAsync();
        var third = cut.Find($"#{Id}-result-3");
        third.GetAttribute("aria-disabled").ShouldBe("true");

        // Act
        await third.ClickAsync();

        // Assert
        values.ShouldBe([1, 2]);
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("You can choose at most 2 images. Remove one to add another.");
    }

    [Fact]
    public async Task A_reorderable_list_moves_an_image_announces_its_place_and_keeps_focus_on_its_button()
    {
        // Arrange
        IReadOnlyList<int> values = [1, 2, 3];
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(1, 2, 3))
            .Add(picker => picker.Values, values)
            .Add(picker => picker.ValuesChanged, changed => values = changed)
            .Add(picker => picker.Reorderable, true));
        cut.Find($"#{Id}-earlier-1").GetAttribute("aria-disabled").ShouldBe("true");
        cut.Find($"#{Id}-later-3").GetAttribute("aria-disabled").ShouldBe("true");
        cut.Find($"#{Id}-earlier-3").GetAttribute("aria-label").ShouldBe("Move Photo 0003 earlier");

        // Act
        await cut.Find($"#{Id}-earlier-3").ClickAsync();
        cut.Render(parameters => parameters.Add(picker => picker.Values, values));

        // Assert
        values.ShouldBe([1, 3, 2]);
        cut.FindAll($"#{Id} ol li span").Select(label => label.TextContent).ShouldBe(["Photo 0001", "Photo 0003", "Photo 0002"]);
        cut.Find($"#{Id}-status").TextContent.Trim().ShouldBe("Photo 0003 moved to position 2 of 3.");
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void Thumbnails_are_decorative_lazy_and_sized_and_only_root_relative_or_https_addresses_are_shown()
    {
        // Arrange
        var catalog = new PickerCatalog<int, PickerImage>(
            image => image.Id,
            [new PickerImage(1, "Local", "/thumbs/1.webp"), new PickerImage(2, "Remote", "https://cdn.example/2.webp"), new PickerImage(3, "Script", "javascript:alert(1)")]);

        // Act
        var cut = this.RenderPicker(_library, parameters => parameters.Add(picker => picker.Catalog, catalog).Add(picker => picker.Values, [1, 2, 3]));

        // Assert
        var images = cut.FindAll($"#{Id} ol img");
        images.Select(image => image.GetAttribute("src")).ShouldBe(["/thumbs/1.webp", "https://cdn.example/2.webp"]);
        foreach (var image in images)
        {
            image.GetAttribute("alt").ShouldBe(string.Empty);
            image.GetAttribute("width").ShouldBe("64");
            image.GetAttribute("height").ShouldBe("48");
            image.GetAttribute("loading").ShouldBe("lazy");
        }
    }

    [Fact]
    public void The_texts_replace_the_english_copy()
    {
        // Arrange & Act
        var cut = this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog(1))
            .Add(picker => picker.Values, [1])
            .Add(picker => picker.Texts, new ImagePickerTexts { Find = "Encontrar uma imagem", Remove = "Remover", RemoveFormat = "Remover {0}" }));

        // Assert
        cut.Find($"#{Id}-find").TextContent.ShouldBe("Encontrar uma imagem");
        cut.Find($"#{Id}-remove-1").TextContent.ShouldBe("Remover");
        cut.Find($"#{Id}-remove-1").GetAttribute("aria-label").ShouldBe("Remover Photo 0001");
    }

    [Fact]
    public void Setting_both_modes_or_a_multiple_mode_option_in_single_mode_is_refused()
    {
        // Arrange & Act
        var both = () => this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Value, 1)
            .Add(picker => picker.Values, [1]));
        var limit = () => this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Value, 1)
            .Add(picker => picker.MaxCount, 3));

        // Assert
        both.ShouldThrow<InvalidOperationException>();
        limit.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void A_list_that_repeats_a_key_is_refused()
    {
        // Arrange & Act
        var render = () => this.RenderPicker(_library, parameters => parameters
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.Values, [1, 2, 1]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImagePicker<int, PickerImage>.Values));
    }

    [Fact]
    public void An_ID_with_white_space_or_a_missing_catalog_is_refused()
    {
        // Arrange & Act
        var id = () => Render<ImagePicker<int, PickerImage>>(parameters => parameters
            .Add(picker => picker.Id, "my photos")
            .Add(picker => picker.Label, "Photos")
            .Add(picker => picker.Search, _library.Search)
            .Add(picker => picker.Catalog, PickerLibrary.Catalog())
            .Add(picker => picker.LabelOf, image => image.Label));
        var catalog = () => this.RenderPicker(_library, parameters => parameters.Add(picker => picker.Values, []));

        // Assert
        id.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImagePicker<int, PickerImage>.Id));
        catalog.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(ImagePicker<int, PickerImage>.Catalog));
    }

    [Fact]
    public void The_first_chosen_page_holds_at_most_one_page_of_keys()
    {
        // Arrange
        int[] chosen = [.. Enumerable.Range(1, 1000)];

        // Act
        var first = ImagePicker.FirstChosenPage(chosen);
        var small = ImagePicker.FirstChosenPage(chosen, pageSize: 3);

        // Assert
        first.ShouldBe(Enumerable.Range(1, 24));
        small.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void A_catalog_finds_remembers_and_lists_the_options_it_knows()
    {
        // Arrange
        var catalog = PickerLibrary.Catalog(1, 2);

        // Act
        catalog.Remember(new PickerImage(2, "Renamed", null));
        catalog.Remember(PickerLibrary.Option(3));

        // Assert
        catalog.Find(2)!.Label.ShouldBe("Renamed");
        catalog.Find(4).ShouldBeNull();
        catalog.Known.Select(image => image.Id).ShouldBe([1, 2, 3]);
        catalog.KeyOf(PickerLibrary.Option(9)).ShouldBe(9);
    }
}
