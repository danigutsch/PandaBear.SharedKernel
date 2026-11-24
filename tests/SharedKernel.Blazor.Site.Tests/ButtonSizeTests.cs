namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the size of a link styled as a button.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ButtonSizeTests : BunitContext
{
    [Fact]
    public void A_regular_button_has_no_size_class()
    {
        // Arrange & Act
        var cut = this.RenderButton(ButtonSize.Regular);

        // Assert
        cut.Find("a").ClassName.ShouldBe("site-button site-button--primary");
    }

    [Fact]
    public void A_compact_button_adds_the_compact_class()
    {
        // Arrange & Act
        var cut = this.RenderButton(ButtonSize.Compact);

        // Assert
        cut.Find("a").ClassName.ShouldBe("site-button site-button--primary site-button--compact");
    }

    [Fact]
    public void An_undefined_size_is_refused()
    {
        // Arrange & Act
        var render = () => this.RenderButton((ButtonSize)9);

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(ButtonLink.Size));
    }

    [Theory]
    [InlineData(ButtonVariant.Primary, ButtonSize.Regular, "site-button site-button--primary")]
    [InlineData(ButtonVariant.Secondary, ButtonSize.Compact, "site-button site-button--secondary site-button--compact")]
    [InlineData(ButtonVariant.Outline, ButtonSize.Regular, "site-button site-button--outline")]
    public void One_style_gives_every_button_look_its_classes(ButtonVariant variant, ButtonSize size, string expected)
    {
        // Arrange & Act
        var classes = ButtonStyle.Classes(variant, size);

        // Assert
        classes.ShouldBe(expected);
    }
}
