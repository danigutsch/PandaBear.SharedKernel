namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the native disclosure.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class DisclosureTests : BunitContext
{
    [Fact]
    public void Disclosure_is_a_native_details_with_a_summary_and_closed_by_default()
    {
        // Arrange & Act
        var cut = Render<Disclosure>(parameters => parameters
            .Add(component => component.Summary, "Itinerary")
            .AddChildContent("<p id=\"body\">Day 1</p>"));

        // Assert
        var details = cut.Find("details");
        details.QuerySelector("summary")!.TextContent.ShouldBe("Itinerary");
        details.QuerySelector(".site-disclosure__content #body").ShouldNotBeNull();
        details.HasAttribute("open").ShouldBeFalse();
    }

    [Fact]
    public void Open_renders_the_open_attribute()
    {
        // Arrange & Act
        var cut = Render<Disclosure>(parameters => parameters
            .Add(component => component.Summary, "Itinerary")
            .Add(component => component.Open, true));

        // Assert
        cut.Find("details").HasAttribute("open").ShouldBeTrue();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_details()
    {
        // Arrange & Act
        var cut = Render<Disclosure>(parameters => parameters
            .Add(component => component.Summary, "Itinerary")
            .Add(component => component.Class, "extra")
            .AddUnmatched("name", "faq"));

        // Assert
        var details = cut.Find("details");
        BunitElementAssertions.HasClass(details.ClassList, "site-disclosure");
        BunitElementAssertions.HasClass(details.ClassList, "extra");
        details.GetAttribute("name").ShouldBe("faq");
    }
}
