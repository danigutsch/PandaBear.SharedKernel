namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the tone and the text alignment of the site footer.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SiteFooterToneTests : BunitContext
{
    [Fact]
    public void A_default_footer_has_no_modifier_class()
    {
        // Arrange & Act
        var cut = Render<SiteFooter>(parameters => parameters.AddChildContent("x"));

        // Assert
        cut.Find("footer").ClassName.ShouldBe("site-footer");
    }

    [Fact]
    public void A_primary_centred_footer_adds_its_modifier_classes_before_the_callers_class()
    {
        // Arrange & Act
        var cut = Render<SiteFooter>(parameters => parameters
            .Add(component => component.Tone, FooterTone.Primary)
            .Add(component => component.TextAlignment, SiteAlignment.Center)
            .Add(component => component.Class, "extra")
            .AddChildContent("x"));

        // Assert
        cut.Find("footer").ClassName.ShouldBe("site-footer site-footer--primary site-footer--text-center extra");
    }

    [Fact]
    public void Undefined_options_are_refused()
    {
        // Arrange & Act
        Action[] renders =
        [
            () => Render<SiteFooter>(parameters => parameters.Add(component => component.Tone, (FooterTone)9)),
            () => Render<SiteFooter>(parameters => parameters.Add(component => component.TextAlignment, (SiteAlignment)9)),
        ];

        // Assert
        renders.ShouldAllSatisfy(render => render.ShouldThrow<ArgumentOutOfRangeException>());
    }
}
