using Microsoft.Playwright;
using PandaBear.Testing.Playwright;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the structural accessibility assertions against real pages in Chromium; the tests skip when the browser is not installed.</summary>
public sealed class PlaywrightAccessibilityTests(ChromiumPageFixture chromium) : IClassFixture<ChromiumPageFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Controls_named_by_a_label_an_aria_label_or_aria_labelledby_pass_and_hidden_and_button_inputs_are_ignored()
    {
        // Arrange
        var page = await chromium.Show("""
            <label for="a">A</label><input id="a">
            <label>B <select name="b"><option>1</option></select></label>
            <input aria-label="C"><span id="d">D</span><textarea aria-labelledby="d"></textarea>
            <input type="hidden" name="h"><input type="submit"><input type="button">
            """);

        // Act, Assert
        await page.ShouldHaveNoUnlabelledControls();
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Unlabelled_controls_are_listed_with_their_tag_and_id_or_name()
    {
        // Arrange
        var page = await chromium.Show("""<input id="email"><select name="country"></select><textarea></textarea><input aria-label=" ">""");
        Func<Task> assertion = page.ShouldHaveNoUnlabelledControls;

        // Act
        var exception = await assertion.ShouldThrow<PlaywrightException>();

        // Assert
        exception.Message.ShouldBe("Expected every form control to have an accessible name, but these have none: input#email; select#country; textarea#?; input#?");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Links_and_buttons_with_text_a_title_or_an_aria_label_pass_and_anchors_without_href_are_ignored()
    {
        // Arrange
        var page = await chromium.Show("""
            <a href="/a">Text</a><button title="T"></button><button aria-label="L"></button><a name="anchor"></a>
            """);

        // Act, Assert
        await page.ShouldHaveNoUnnamedLinksOrButtons();
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Unnamed_links_and_buttons_are_listed_with_the_start_of_their_markup()
    {
        // Arrange
        var page = await chromium.Show("""<a href="/x"> </a><button class="icon"></button>""");
        Func<Task> assertion = page.ShouldHaveNoUnnamedLinksOrButtons;

        // Act
        var exception = await assertion.ShouldThrow<PlaywrightException>();

        // Assert
        exception.Message.ShouldBe("""Expected every link and button to have an accessible name, but these have none: <a href="/x"> </a>; <button class="icon"></button>""");
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData("", 0)]
    [InlineData("<h1>A</h1><h1>B</h1>", 2)]
    public async Task A_page_without_exactly_one_level_one_heading_fails_and_reports_the_count(string body, int count)
    {
        // Arrange
        var page = await chromium.Show(body);
        Func<Task> assertion = page.ShouldHaveSingleLevelOneHeading;

        // Act
        var exception = await assertion.ShouldThrow<PlaywrightException>();

        // Assert
        exception.Message.ShouldBe($"Expected exactly one first-level heading (h1), but found {count}.");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_page_with_one_level_one_heading_passes()
    {
        // Arrange
        var page = await chromium.Show("<h1>Title</h1><h2>Section</h2>");

        // Act, Assert
        await page.ShouldHaveSingleLevelOneHeading();
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_document_language_is_compared_ignoring_case_and_a_difference_is_reported()
    {
        // Arrange
        var page = await chromium.Show("<p>Olá</p>", language: "pt-BR");
        Func<Task> wrong = () => page.ShouldHaveLanguage("en");

        // Act
        await page.ShouldHaveLanguage("PT-br");
        var exception = await wrong.ShouldThrow<PlaywrightException>();

        // Assert
        exception.Message.ShouldBe("Expected the document language to be 'en', but it is 'pt-BR'.");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Horizontal_scroll_is_reported_at_a_narrow_viewport_and_passes_at_a_wide_one()
    {
        // Arrange
        var page = await chromium.Show("""<div style="width:600px;height:10px">wide</div>""");
        await page.SetViewportSizeAsync(375, 800);
        Func<Task> narrow = page.ShouldNotScrollHorizontally;

        // Act
        var exception = await narrow.ShouldThrow<PlaywrightException>();
        await page.SetViewportSizeAsync(1280, 800);
        await page.ShouldNotScrollHorizontally();

        // Assert
        exception.Message.ShouldBe("Expected the page not to scroll horizontally, but its content is wider than the viewport.");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Focus_inside_a_container_passes_and_focus_elsewhere_names_the_focused_element()
    {
        // Arrange
        var page = await chromium.Show("""<section role="alert"><button id="in">Retry</button></section><input id="out" aria-label="Out">""");
        Func<Task> assertion = () => page.ShouldHaveFocusWithin("[role='alert']");

        // Act
        await page.FocusAsync("#in");
        await page.ShouldHaveFocusWithin("[role='alert']");
        await page.FocusAsync("#out");
        var exception = await assertion.ShouldThrow<PlaywrightException>();

        // Assert
        exception.Message.ShouldBe("""Expected keyboard focus inside '[role='alert']', but it is on: <input id="out" aria-label="Out">""");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_well_structured_page_has_an_accessible_structure()
    {
        // Arrange
        var page = await chromium.Show("""<h1>Customers</h1><label>Name <input></label><a href="/new">New</a>""");

        // Act, Assert
        await page.ShouldHaveAccessibleStructure();
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_accessible_structure_check_reports_every_problem_in_one_failure()
    {
        // Arrange
        var page = await chromium.Show("""<input id="x"><button></button>""");
        Func<Task> assertion = page.ShouldHaveAccessibleStructure;

        // Act
        var exception = await assertion.ShouldThrow<PlaywrightException>();

        // Assert
        exception.Message.ShouldBe(string.Join(
            Environment.NewLine,
            "Expected every form control to have an accessible name, but these have none: input#x",
            "Expected every link and button to have an accessible name, but these have none: <button></button>",
            "Expected exactly one first-level heading (h1), but found 0."));
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_message_overload_states_the_reason_before_the_failure()
    {
        // Arrange
        var page = await chromium.Show("<p>none</p>");
        Func<Task> assertion = () => page.ShouldHaveSingleLevelOneHeading("Every page names its topic.");

        // Act
        var exception = await assertion.ShouldThrow<PlaywrightException>();

        // Assert
        exception.Message.ShouldBe("Every page names its topic." + Environment.NewLine + "Expected exactly one first-level heading (h1), but found 0.");
    }

    [Fact]
    public async Task Assertions_reject_a_null_page_message_selector_and_language()
    {
        // Arrange
        IPage page = TitleAssertionPageProxy.Page;
        IPage? missingPage = null;
        Func<Task> nullPage = () => missingPage!.ShouldHaveAccessibleStructure();
        Func<Task> nullPageWithMessage = () => missingPage!.ShouldHaveSingleLevelOneHeading("reason");
        Func<Task> nullMessage = () => page.ShouldHaveSingleLevelOneHeading(null!);
        Func<Task> blankSelector = () => page.ShouldHaveFocusWithin(" ");
        Func<Task> nullLanguage = () => page.ShouldHaveLanguage(null!);

        // Act
        var pageException = await nullPage.ShouldThrow<ArgumentNullException>();
        var pageMessageException = await nullPageWithMessage.ShouldThrow<ArgumentNullException>();
        var messageException = await nullMessage.ShouldThrow<ArgumentNullException>();
        var selectorException = await blankSelector.ShouldThrow<ArgumentException>();
        var languageException = await nullLanguage.ShouldThrow<ArgumentNullException>();

        // Assert
        pageException.ParamName.ShouldBe("page");
        pageMessageException.ParamName.ShouldBe("page");
        messageException.ParamName.ShouldBe("userMessage");
        selectorException.ParamName.ShouldBe("containerSelector");
        languageException.ParamName.ShouldBe("expectedLanguage");
    }
}
