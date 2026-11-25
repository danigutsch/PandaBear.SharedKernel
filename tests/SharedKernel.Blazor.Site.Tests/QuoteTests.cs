namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the testimonial quote.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class QuoteTests : BunitContext
{
    [Fact]
    public void Quote_is_a_blockquote_with_a_cite_caption()
    {
        // Arrange & Act
        var cut = Render<Quote>(parameters => parameters
            .Add(component => component.Cite, "Ana, Lisbon")
            .AddChildContent("Unforgettable."));

        // Assert
        cut.Find("figure > blockquote").TextContent.ShouldBe("Unforgettable.");
        cut.Find("figure > figcaption > cite").TextContent.ShouldBe("Ana, Lisbon");
    }

    [Fact]
    public void Caption_is_omitted_without_a_cite_and_attributes_pass_through()
    {
        // Arrange & Act
        var cut = Render<Quote>(parameters => parameters
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x")
            .AddChildContent("Great."));

        // Assert
        cut.FindAll("figcaption").ShouldBeEmpty();
        var figure = cut.Find("figure");
        BunitElementAssertions.HasClass(figure.ClassList, "site-quote");
        BunitElementAssertions.HasClass(figure.ClassList, "extra");
        figure.GetAttribute("data-test").ShouldBe("x");
    }
}
