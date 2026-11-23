namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the status badge.</summary>
public sealed class StatusBadgeTests : BunitContext
{
    [Fact]
    public void A_badge_is_strong_text_with_the_neutral_tone_by_default()
    {
        // Arrange & Act
        var cut = Render<StatusBadge>(parameters => parameters.Add(component => component.Text, "Open"));

        // Assert
        var badge = cut.Find("strong");
        badge.ClassName.ShouldBe("site-status site-status--neutral");
        badge.TextContent.ShouldBe("Open");
        badge.Children.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(StatusTone.Neutral, "site-status site-status--neutral")]
    [InlineData(StatusTone.Info, "site-status site-status--info")]
    [InlineData(StatusTone.Success, "site-status site-status--success")]
    [InlineData(StatusTone.Warning, "site-status site-status--warning")]
    [InlineData(StatusTone.Danger, "site-status site-status--danger")]
    public void The_tone_sets_a_class(StatusTone tone, string classes)
    {
        // Arrange & Act
        var cut = Render<StatusBadge>(parameters => parameters
            .Add(component => component.Text, "Few places left")
            .Add(component => component.Tone, tone));

        // Assert
        cut.Find("strong").ClassName.ShouldBe(classes);
    }

    [Fact]
    public void A_label_is_a_visually_hidden_prefix_followed_by_a_space()
    {
        // Arrange & Act
        var cut = Render<StatusBadge>(parameters => parameters
            .Add(component => component.Text, "Sold out")
            .Add(component => component.Label, "Status:")
            .Add(component => component.Tone, StatusTone.Danger));

        // Assert
        var badge = cut.Find("strong");
        var prefix = badge.FirstElementChild.ShouldNotBeNull();
        prefix.TagName.ShouldBe("SPAN");
        prefix.ClassName.ShouldBe("site-visually-hidden");
        prefix.TextContent.ShouldBe("Status: ");
        badge.TextContent.ShouldBe("Status: Sold out");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_label_renders_no_prefix(string? label)
    {
        // Arrange & Act
        var cut = Render<StatusBadge>(parameters => parameters
            .Add(component => component.Text, "Open")
            .Add(component => component.Label, label));

        // Assert
        cut.Find("strong").Children.ShouldBeEmpty();
    }

    [Fact]
    public void A_badge_is_never_interactive()
    {
        // Arrange & Act
        var cut = Render<StatusBadge>(parameters => parameters
            .Add(component => component.Text, "Open")
            .Add(component => component.Label, "Status:"));

        // Assert
        cut.FindAll("a, button, [tabindex], [role]").ShouldBeEmpty();
        cut.Find("strong").HasAttribute("tabindex").ShouldBeFalse();
    }

    [Fact]
    public void Class_and_attributes_reach_the_badge()
    {
        // Arrange & Act
        var cut = Render<StatusBadge>(parameters => parameters
            .Add(component => component.Text, "Open")
            .Add(component => component.Class, "tour-status")
            .AddUnmatched("data-state", "open"));

        // Assert
        var badge = cut.Find("strong");
        badge.ClassName.ShouldBe("site-status site-status--neutral tour-status");
        badge.GetAttribute("data-state").ShouldBe("open");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_text_is_refused(string text)
    {
        // Arrange & Act
        var render = () => Render<StatusBadge>(parameters => parameters.Add(component => component.Text, text));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(StatusBadge.Text));
    }

    [Fact]
    public void An_undefined_tone_is_refused()
    {
        // Arrange & Act
        var render = () => Render<StatusBadge>(parameters => parameters
            .Add(component => component.Text, "Open")
            .Add(component => component.Tone, (StatusTone)9));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(StatusBadge.Tone));
    }
}
