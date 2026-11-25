namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the site footer.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SiteFooterTests : BunitContext
{
    [Fact]
    public void Footer_is_a_contentinfo_landmark_with_content_and_legal_slots()
    {
        // Arrange & Act
        var cut = Render<SiteFooter>(parameters => parameters
            .AddChildContent("<p id=\"contact\">Contact</p>")
            .Add(component => component.Legal, "<small id=\"legal\">Legal</small>"));

        // Assert
        cut.Find("footer .site-footer__content #contact").ShouldNotBeNull();
        cut.Find("footer .site-footer__legal #legal").ShouldNotBeNull();
    }

    [Fact]
    public void Legal_area_is_omitted_without_legal_content()
    {
        // Arrange & Act
        var cut = Render<SiteFooter>(parameters => parameters.AddChildContent("x"));

        // Assert
        cut.FindAll(".site-footer__legal").ShouldBeEmpty();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_footer()
    {
        // Arrange & Act
        var cut = Render<SiteFooter>(parameters => parameters
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x"));

        // Assert
        var footer = cut.Find("footer");
        BunitElementAssertions.HasClass(footer.ClassList, "site-footer");
        BunitElementAssertions.HasClass(footer.ClassList, "extra");
        footer.GetAttribute("data-test").ShouldBe("x");
    }
}
