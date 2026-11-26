namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the section with its heading, intro and divider.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SectionTests : BunitContext
{
    private const string MarkupWithoutEyebrow =
        "<section class=\"site-section\" aria-labelledby=\"tours-heading\">\n        "
        + "<div class=\"site-section__header\"><h2 id=\"tours-heading\" class=\"site-section__title\">Tours</h2>"
        + "<div class=\"site-divider\" aria-hidden=\"true\"></div><p class=\"site-section__intro\">Pick one.</p></div>\n        "
        + "<div class=\"site-section__body\"><p>Body</p></div>\n    </section>";

    [Fact]
    public void Section_has_a_level_two_heading_intro_and_body_and_no_generated_ID()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.Intro, "Pick one.")
            .AddChildContent("<p id=\"body\">Body</p>"));

        // Assert
        var heading = cut.Find("section > .site-section__header > h2");
        heading.TextContent.ShouldBe("Tours");
        heading.HasAttribute("id").ShouldBeFalse();
        cut.Find("section").HasAttribute("aria-labelledby").ShouldBeFalse();
        cut.Find(".site-section__intro").TextContent.ShouldBe("Pick one.");
        cut.Find(".site-section__body #body").ShouldNotBeNull();
    }

    [Fact]
    public void Section_markup_is_the_same_on_every_render_so_prerendering_and_the_circuit_agree()
    {
        // Arrange & Act
        var first = Render<Section>(parameters => parameters.Add(component => component.Heading, "Tours"));
        var second = Render<Section>(parameters => parameters.Add(component => component.Heading, "Tours"));

        // Assert
        second.Markup.ShouldBe(first.Markup);
    }

    [Fact]
    public void Without_an_eyebrow_the_heading_is_the_first_child_of_the_header()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.HeadingId, "tours-heading")
            .Add(component => component.Intro, "Pick one.")
            .AddChildContent("<p>Body</p>"));

        // Assert
        cut.Markup.ShouldBe(MarkupWithoutEyebrow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void A_missing_or_blank_eyebrow_leaves_the_markup_unchanged(string? eyebrow)
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Eyebrow, eyebrow)
            .Add(component => component.Heading, "Tours")
            .Add(component => component.HeadingId, "tours-heading")
            .Add(component => component.Intro, "Pick one.")
            .AddChildContent("<p>Body</p>"));

        // Assert
        cut.Markup.ShouldBe(MarkupWithoutEyebrow);
        cut.FindAll("hgroup").ShouldBeEmpty();
        cut.FindAll(".site-section__eyebrow").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void An_eyebrow_comes_before_the_heading_in_an_hgroup_that_opens_the_header(int level)
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Eyebrow, "Adventure")
            .Add(component => component.Heading, "By Vespa through Italy")
            .Add(component => component.HeadingId, "italy-heading")
            .Add(component => component.Level, level)
            .Add(component => component.Intro, "Ten days."));

        // Assert
        var header = cut.Find("section > .site-section__header");
        header.Children.Select(child => child.ClassName).ShouldBe(["site-section__heading", "site-divider", "site-section__intro"]);
        var heading = header.Children[0];
        heading.TagName.ShouldBe("HGROUP");
        heading.Children.Select(child => child.TagName).ShouldBe(["P", $"H{level}"]);
        heading.Children[0].ClassName.ShouldBe("site-section__eyebrow");
        heading.Children[0].TextContent.ShouldBe("Adventure");
        heading.Children[1].ClassName.ShouldBe("site-section__title");
        heading.Children[1].Id.ShouldBe("italy-heading");
        heading.Children[1].TextContent.ShouldBe("By Vespa through Italy");
        cut.Find("section").GetAttribute("aria-labelledby").ShouldBe("italy-heading");
    }

    [Fact]
    public void Eyebrow_text_is_encoded()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Eyebrow, "<b>New</b> & safe")
            .Add(component => component.Heading, "Tours"));

        // Assert
        cut.FindAll("b").ShouldBeEmpty();
        cut.Find(".site-section__eyebrow").TextContent.ShouldBe("<b>New</b> & safe");
    }

    [Fact]
    public void A_heading_ID_labels_the_section_by_its_heading()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.HeadingId, "tours-heading"));

        // Assert
        cut.Find("h2").Id.ShouldBe("tours-heading");
        cut.Find("section").GetAttribute("aria-labelledby").ShouldBe("tours-heading");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("two words")]
    [InlineData("two\u00A0words")]
    [InlineData("two\u2003words")]
    public void A_blank_or_spaced_heading_ID_is_rejected(string id)
    {
        // Arrange & Act
        var render = () => Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.HeadingId, id));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Section.HeadingId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void Heading_level_is_configurable(int level)
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.Level, level));

        // Assert
        cut.Find($"h{level}").TextContent.ShouldBe("Tours");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Heading_level_outside_one_to_six_is_rejected(int level)
    {
        // Arrange & Act
        var render = () => Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.Level, level));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Divider_is_decorative_and_can_be_switched_off()
    {
        // Arrange & Act
        var with = Render<Section>(parameters => parameters.Add(component => component.Heading, "Tours"));
        var without = Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.Divider, false));

        // Assert
        with.Find(".site-divider").GetAttribute("aria-hidden").ShouldBe("true");
        without.FindAll(".site-divider").ShouldBeEmpty();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_section()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.Class, "extra")
            .AddUnmatched("id", "tours"));

        // Assert
        var section = cut.Find("section");
        BunitElementAssertions.HasClass(section.ClassList, "site-section");
        BunitElementAssertions.HasClass(section.ClassList, "extra");
        section.Id.ShouldBe("tours");
    }

    [Fact]
    public void A_section_without_media_keeps_its_header_and_body_as_children()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters.Add(component => component.Heading, "Trips"));

        // Assert
        var section = cut.Find("section");
        section.ClassName.ShouldBe("site-section");
        section.Children.Select(child => child.ClassName).ShouldBe(["site-section__header", "site-section__body"]);
    }

    [Fact]
    public void Media_splits_the_section_into_the_text_then_the_media_in_reading_order()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Past trips")
            .Add(component => component.Media, "<img id=\"photo\" src=\"/p.jpg\" alt=\"\">")
            .AddChildContent("<a id=\"more\" href=\"/trips\">More</a>"));

        // Assert
        var section = cut.Find("section");
        section.ClassName.ShouldBe("site-section");
        var split = section.Children.ShouldHaveSingleItem();
        split.ClassName.ShouldBe("site-split");
        split.Children.Select(child => child.ClassName).ShouldBe(["site-split__main", "site-split__media"]);
        split.Children[0].Children.Select(child => child.ClassName).ShouldBe(["site-section__header", "site-section__body"]);
        cut.Find(".site-split__main #more").ShouldNotBeNull();
        cut.Find(".site-split__media #photo").ShouldNotBeNull();
        cut.FindAll("aside").ShouldBeEmpty();
    }

    [Fact]
    public void An_eyebrow_in_a_split_section_stays_in_the_header_of_the_text_column()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Eyebrow, "Journal")
            .Add(component => component.Heading, "Past trips")
            .Add(component => component.Media, "<img id=\"photo\" src=\"/p.jpg\" alt=\"\">"));

        // Assert
        var main = cut.Find("section > .site-split > .site-split__main");
        main.Children.Select(child => child.ClassName).ShouldBe(["site-section__header", "site-section__body"]);
        var heading = main.Children[0].Children[0];
        heading.TagName.ShouldBe("HGROUP");
        heading.Children.Select(child => child.ClassName).ShouldBe(["site-section__eyebrow", "site-section__title"]);
        cut.FindAll(".site-split__media hgroup").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, "site-section extra")]
    [InlineData(SiteAlignment.Start, "site-section site-section--text-start extra")]
    [InlineData(SiteAlignment.Center, "site-section site-section--text-center extra")]
    public void The_text_alignment_inherits_by_default_and_otherwise_sets_a_class_before_the_extra_class(SiteAlignment? alignment, string classes)
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.TextAlignment, alignment)
            .Add(component => component.Class, "extra"));

        // Assert
        cut.Find("section").ClassName.ShouldBe(classes);
    }

    [Fact]
    public void An_undefined_text_alignment_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.TextAlignment, (SiteAlignment)5));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(Section.TextAlignment));
    }
}
