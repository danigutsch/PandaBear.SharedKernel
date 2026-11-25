using System.Globalization;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the numbered list of steps, such as the days of an itinerary.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class StepListTests : BunitContext
{
    private static readonly FactCard[] Facts = [new("Distance", [new("42 km")])];

    /// <summary>Gets steps that are missing, have a blank title, a missing paragraph list or fact list, or a blank paragraph.</summary>
    public static TheoryData<StepItem?> InvalidSteps { get; } = new()
    {
        null!,
        new StepItem(" ", ["Text."], null, null, []),
        new StepItem("Arrival", null!, null, null, []),
        new StepItem("Arrival", ["Text.", " "], null, null, []),
        new StepItem("Arrival", ["Text."], null, null, null!),
        new StepItem("Arrival", ["Text."], null, null, [null!]),
    };

    [Fact]
    public void A_step_list_is_an_ordered_list_with_an_explicit_list_role()
    {
        // Arrange & Act
        var cut = this.RenderSteps([StepRendering.Step("Arrival"), StepRendering.Step("Valley of the Moon")]);

        // Assert
        var list = cut.Find("ol");
        list.ClassName.ShouldBe("site-step-list");
        list.GetAttribute("role").ShouldBe("list");
        list.Children.Select(child => child.TagName).ShouldBe(["LI", "LI"]);
        list.Children.Select(child => child.ClassName).ShouldBe(["site-step-list__item", "site-step-list__item"]);
    }

    [Fact]
    public void Each_step_opens_with_an_hgroup_of_its_number_from_the_position_then_its_title_heading()
    {
        // Arrange & Act
        var cut = this.RenderSteps([StepRendering.Step("Arrival"), StepRendering.Step("Valley of the Moon")]);

        // Assert
        var groups = cut.FindAll("li > hgroup.site-step-list__heading");
        groups.Count.ShouldBe(2);
        groups.Select(group => group.Children.Select(child => child.TagName).ToArray()).ShouldBe([["P", "H3"], ["P", "H3"]]);
        cut.FindAll(".site-step-list__number").Select(number => number.TextContent).ShouldBe(["Day 1", "Day 2"]);
        cut.FindAll("h3.site-step-list__title").Select(title => title.TextContent).ShouldBe(["Arrival", "Valley of the Moon"]);
        cut.FindAll("h3[id]").ShouldBeEmpty();
    }

    [Fact]
    public void A_step_holds_its_inline_facts_its_note_then_its_paragraphs_in_that_order()
    {
        // Arrange & Act
        var cut = this.RenderSteps([new StepItem("Arrival", ["Transfer to the hotel.", "Dinner together."], "Long drive", null, Facts)]);

        // Assert
        var item = cut.Find("li");
        item.Children.Select(child => child.TagName + "." + child.ClassName).ShouldBe(
        [
            "HGROUP.site-step-list__heading",
            "DL.site-fact-grid site-fact-grid--inline site-step-list__facts",
            "P.site-step-list__note",
            "P.",
            "P.",
        ]);
        item.Children[2].TextContent.ShouldBe("Long drive");
        item.Children.Skip(3).Select(child => child.TextContent).ShouldBe(["Transfer to the hotel.", "Dinner together."]);
    }

    [Fact]
    public void A_step_without_facts_or_note_renders_neither()
    {
        // Arrange & Act
        var cut = this.RenderSteps([new StepItem("Arrival", ["Transfer."], " ", null, [])]);

        // Assert
        cut.FindAll("dl, .site-step-list__note").ShouldBeEmpty();
    }

    [Fact]
    public void A_step_with_an_image_puts_its_text_beside_the_image_in_a_split_that_starts_at_the_top()
    {
        // Arrange & Act
        var cut = this.RenderSteps([StepRendering.Step("Arrival", image: true)]);

        // Assert
        var split = cut.Find("li > .site-split");
        split.ClassName.ShouldBe("site-split");
        cut.Find(".site-split__main > hgroup").ShouldNotBeNull();
        var figure = cut.Find(".site-split__media > .site-image-grid > figure");
        figure.QuerySelector("img")!.GetAttribute("loading").ShouldBe("lazy");
    }

    [Fact]
    public void The_image_sides_alternate_from_image_to_image_so_the_photos_zigzag()
    {
        // Arrange & Act
        var cut = this.RenderSteps([StepRendering.Step("One", image: true), StepRendering.Step("Two"), StepRendering.Step("Three", image: true), StepRendering.Step("Four", image: true)]);

        // Assert
        cut.FindAll(".site-split").Select(split => split.ClassName).ShouldBe(["site-split", "site-split site-split--media-start", "site-split"]);
    }

    [Fact]
    public void Without_alternation_every_image_is_at_the_end()
    {
        // Arrange & Act
        var cut = this.RenderSteps([StepRendering.Step("One", image: true), StepRendering.Step("Two", image: true)], parameters => parameters.Add(component => component.Alternate, false));

        // Assert
        cut.FindAll(".site-split").Select(split => split.ClassName).ShouldBe(["site-split", "site-split"]);
    }

    [Fact]
    public void The_first_eager_count_images_load_eagerly_and_the_rest_lazily()
    {
        // Arrange & Act
        var cut = this.RenderSteps([StepRendering.Step("One", image: true), StepRendering.Step("Two"), StepRendering.Step("Three", image: true), StepRendering.Step("Four", image: true)], parameters => parameters.Add(component => component.EagerCount, 2));

        // Assert
        cut.FindAll("img").Select(image => image.GetAttribute("loading")).ShouldBe(["eager", "eager", "lazy"]);
    }

    [Fact]
    public void The_heading_level_and_the_heading_ID_format_reach_every_title()
    {
        // Arrange & Act
        var cut = this.RenderSteps([StepRendering.Step("Arrival"), StepRendering.Step("Valley")], parameters => parameters
            .Add(component => component.HeadingLevel, 4)
            .Add(component => component.HeadingIdFormat, "day-{0}"));

        // Assert
        cut.FindAll("h3").ShouldBeEmpty();
        cut.FindAll("h4.site-step-list__title").Select(title => title.Id).ShouldBe(["day-1", "day-2"]);
    }

    [Fact]
    public void The_number_is_formatted_with_the_current_culture()
    {
        // Arrange & Act
        var numbers = Testing.CultureScope.Run(CultureInfo.GetCultureInfo("ar-SA"), () =>
            this.RenderSteps([StepRendering.Step("Arrival")], numberFormat: "{0:N0}")
                .FindAll(".site-step-list__number").Select(number => number.TextContent).ToArray());

        // Assert
        numbers.ShouldBe([1.ToString("N0", CultureInfo.GetCultureInfo("ar-SA"))]);
    }

    [Fact]
    public void Class_and_attributes_reach_the_list()
    {
        // Arrange & Act
        var cut = this.RenderSteps([StepRendering.Step("Arrival")], parameters => parameters
            .Add(component => component.Class, "itinerary")
            .AddUnmatched("aria-labelledby", "itinerary-heading"));

        // Assert
        var list = cut.Find("ol");
        list.ClassName.ShouldBe("site-step-list itinerary");
        list.GetAttribute("aria-labelledby").ShouldBe("itinerary-heading");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Day")]
    [InlineData("Day {1}")]
    [InlineData("Day {{0}}")]
    public void A_number_format_without_the_position_is_refused(string format)
    {
        // Arrange & Act
        var render = () => this.RenderSteps([StepRendering.Step("Arrival")], numberFormat: format);

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(StepList.NumberFormat));
    }

    [Theory]
    [InlineData("")]
    [InlineData("day")]
    [InlineData("day {0}")]
    [InlineData("day-{1}")]
    [InlineData("day-{{0}}")]
    public void A_heading_ID_format_that_gives_no_unique_ID_without_white_space_is_refused(string format)
    {
        // Arrange & Act
        var render = () => this.RenderSteps([StepRendering.Step("Arrival")], parameters => parameters.Add(component => component.HeadingIdFormat, format));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(StepList.HeadingIdFormat));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void A_heading_level_outside_1_to_6_is_refused(int level)
    {
        // Arrange & Act
        var render = () => this.RenderSteps([StepRendering.Step("Arrival")], parameters => parameters.Add(component => component.HeadingLevel, level));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(StepList.HeadingLevel));
    }

    [Fact]
    public void A_negative_eager_count_is_refused()
    {
        // Arrange & Act
        var render = () => this.RenderSteps([StepRendering.Step("Arrival")], parameters => parameters.Add(component => component.EagerCount, -1));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(StepList.EagerCount));
    }

    [Theory]
    [MemberData(nameof(InvalidSteps))]
    public void An_invalid_step_is_refused(StepItem? step)
    {
        // Arrange & Act
        var render = () => this.RenderSteps([step!]);

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(StepList.Steps));
    }
}
