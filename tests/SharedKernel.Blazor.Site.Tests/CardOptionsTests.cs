namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the typed look of a card: style, image aspect and shadow, alignment, eyebrow and the more look.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class CardOptionsTests : BunitContext
{
    [Fact]
    public void A_default_card_has_no_modifier_class()
    {
        // Arrange & Act
        var cut = this.RenderCard();

        // Assert
        cut.Find("article").ClassName.ShouldBe("site-card");
        cut.FindAll(".site-card__eyebrow, .site-card__more, .site-action-row").ShouldBeEmpty();
    }

    [Fact]
    public void A_plain_card_drops_its_box()
    {
        // Arrange & Act
        var cut = this.RenderCard(parameters => parameters.Add(component => component.CardStyle, CardStyle.Plain));

        // Assert
        BunitElementAssertions.HasClass(cut.Find("article").ClassList, "site-card--plain");
    }

    [Fact]
    public void A_style_attribute_reaches_the_article_because_no_parameter_takes_its_name()
    {
        // Arrange & Act
        var cut = this.RenderCard(parameters => parameters
            .AddUnmatched("style", "view-transition-name: tour")
            .Add(component => component.CardStyle, CardStyle.Plain));

        // Assert
        var article = cut.Find("article");
        article.GetAttribute("style").ShouldBe("view-transition-name: tour");
        BunitElementAssertions.HasClass(article.ClassList, "site-card--plain");
    }

    [Theory]
    [InlineData(CardImageAspect.SixteenByNine, "site-card--aspect-16x9")]
    [InlineData(CardImageAspect.Square, "site-card--aspect-1x1")]
    [InlineData(CardImageAspect.Natural, "site-card--aspect-natural")]
    public void The_image_aspect_is_a_modifier_class(CardImageAspect aspect, string expected)
    {
        // Arrange & Act
        var cut = this.RenderCard(parameters => parameters.Add(component => component.ImageAspect, aspect));

        // Assert
        BunitElementAssertions.HasClass(cut.Find("article").ClassList, expected);
    }

    [Fact]
    public void A_plain_card_takes_an_offset_image_shadow()
    {
        // Arrange & Act
        var cut = this.RenderCard(parameters => parameters
            .Add(component => component.CardStyle, CardStyle.Plain)
            .Add(component => component.ImageShadow, CardImageShadow.Offset));

        // Assert
        BunitElementAssertions.HasClass(cut.Find("article").ClassList, "site-card--image-shadow");
    }

    [Fact]
    public void A_boxed_card_refuses_an_offset_image_shadow()
    {
        // Arrange & Act
        var render = () => this.RenderCard(parameters => parameters.Add(component => component.ImageShadow, CardImageShadow.Offset));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Card.ImageShadow));
    }

    [Fact]
    public void A_centred_card_centres_its_text()
    {
        // Arrange & Act
        var cut = this.RenderCard(parameters => parameters.Add(component => component.TextAlignment, SiteAlignment.Center));

        // Assert
        BunitElementAssertions.HasClass(cut.Find("article").ClassList, "site-card--text-center");
    }

    [Theory]
    [InlineData(SiteAlignment.Start, "site-action-row site-card__actions")]
    [InlineData(SiteAlignment.Center, "site-action-row site-action-row--center site-card__actions")]
    public void The_action_row_is_justified_by_the_card_alignment(SiteAlignment alignment, string expected)
    {
        // Arrange & Act
        var cut = this.RenderCard(parameters => parameters
            .Add(component => component.TextAlignment, alignment)
            .Add(component => component.MoreText, "More"));

        // Assert
        cut.Find(".site-card__actions").ClassName.ShouldBe(expected);
    }

    [Fact]
    public void An_eyebrow_goes_above_the_title_in_one_heading_group()
    {
        // Arrange & Act
        var cut = this.RenderCard(parameters => parameters.Add(component => component.Eyebrow, "Cycling"));

        // Assert
        var group = cut.Find(".site-card__body > hgroup.site-card__heading");
        group.Children.Select(child => child.TagName).ShouldBe(["P", "H3"]);
        group.Children[0].ClassName.ShouldBe("site-card__eyebrow");
        group.Children[0].TextContent.ShouldBe("Cycling");
        group.Children[1].ClassName.ShouldBe("site-card__title");
    }

    [Fact]
    public void The_more_look_is_a_hidden_outline_button_span_first_in_the_action_row()
    {
        // Arrange & Act
        var cut = this.RenderCard(parameters => parameters
            .Add(component => component.MoreText, "Learn more")
            .Add(component => component.Actions, "<a id=\"sign-up\" class=\"site-button\" href=\"/sign-up\">Sign up</a>"));

        // Assert
        var row = cut.Find("article > .site-action-row.site-card__actions");
        row.Children.Length.ShouldBe(2);
        var more = row.Children[0];
        more.TagName.ShouldBe("SPAN");
        more.ClassName.ShouldBe("site-button site-button--outline site-card__more");
        more.GetAttribute("aria-hidden").ShouldBe("true");
        more.TextContent.ShouldBe("Learn more");
        row.Children[1].Id.ShouldBe("sign-up");
        cut.FindAll("a").Count.ShouldBe(2);
    }

    [Fact]
    public void The_more_look_takes_the_classes_of_a_button_link_of_the_same_variant_and_size()
    {
        // Arrange
        var card = this.RenderCard(parameters => parameters
            .Add(component => component.MoreText, "More")
            .Add(component => component.MoreVariant, ButtonVariant.Primary)
            .Add(component => component.MoreSize, ButtonSize.Compact));

        // Act
        var button = Render<ButtonLink>(parameters => parameters
            .Add(component => component.Href, SiteFixtures.Link("/x"))
            .Add(component => component.Variant, ButtonVariant.Primary)
            .Add(component => component.Size, ButtonSize.Compact)
            .AddChildContent("More"));

        // Assert
        card.Find(".site-card__more").ClassName.ShouldBe(button.Find("a").ClassName + " site-card__more");
        button.Find("a").ClassName.ShouldBe("site-button site-button--primary site-button--compact");
    }

    [Fact]
    public void Actions_without_a_more_look_are_the_action_row()
    {
        // Arrange & Act
        var cut = this.RenderCard(parameters => parameters.Add(component => component.Actions, "<a id=\"more\" href=\"/x\">More</a>"));

        // Assert
        cut.Find(".site-card__actions.site-action-row > #more").ShouldNotBeNull();
        cut.FindAll(".site-card__more").ShouldBeEmpty();
    }

    [Fact]
    public void The_more_look_needs_the_title_link_it_stands_for()
    {
        // Arrange & Act
        var render = () => Render<Card>(parameters => parameters
            .Add(component => component.Title, "Atacama")
            .Add(component => component.MoreText, "Learn more"));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Card.MoreText));
    }

    [Fact]
    public void Undefined_options_are_refused()
    {
        // Arrange & Act
        Action[] renders =
        [
            () => this.RenderCard(parameters => parameters.Add(component => component.CardStyle, (CardStyle)9)),
            () => this.RenderCard(parameters => parameters.Add(component => component.ImageAspect, (CardImageAspect)9)),
            () => this.RenderCard(parameters => parameters.Add(component => component.ImageShadow, (CardImageShadow)9)),
            () => this.RenderCard(parameters => parameters.Add(component => component.TextAlignment, (SiteAlignment)9)),
            () => this.RenderCard(parameters => parameters.Add(component => component.MoreSize, (ButtonSize)9)),
            () => this.RenderCard(parameters => parameters.Add(component => component.MoreVariant, (ButtonVariant)9)),
        ];

        // Assert
        renders.ShouldAllSatisfy(render => render.ShouldThrow<ArgumentOutOfRangeException>());
    }
}
