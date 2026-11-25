namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the facts table.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class FactsTableTests : BunitContext
{
    [Fact]
    public void Table_has_a_caption_and_row_headers_with_scope()
    {
        // Arrange & Act
        var cut = Render<FactsTable>(parameters => parameters
            .Add(component => component.Caption, "Trip facts")
            .Add(component => component.Rows, [new FactRow("Duration", "8 days"), new FactRow("Group", "Up to 12")]));

        // Assert
        cut.Find("table > caption").TextContent.ShouldBe("Trip facts");
        var headers = cut.FindAll("tbody tr > th");
        headers.Select(header => header.TextContent).ShouldBeEquivalentTo("Duration", "Group");
        headers.ShouldAllSatisfy(header => header.GetAttribute("scope").ShouldBe("row"));
        cut.FindAll("tbody tr > td").Select(cell => cell.TextContent).ShouldBeEquivalentTo("8 days", "Up to 12");
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_table()
    {
        // Arrange & Act
        var cut = Render<FactsTable>(parameters => parameters
            .Add(component => component.Caption, "Facts")
            .Add(component => component.Rows, [])
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x"));

        // Assert
        var table = cut.Find("table");
        BunitElementAssertions.HasClass(table.ClassList, "site-facts");
        BunitElementAssertions.HasClass(table.ClassList, "extra");
        table.GetAttribute("data-test").ShouldBe("x");
    }

    [Fact]
    public void A_blank_caption_is_rejected_because_the_table_would_have_no_name()
    {
        // Arrange & Act
        var render = () => Render<FactsTable>(parameters => parameters
            .Add(component => component.Caption, " ")
            .Add(component => component.Rows, []));

        // Assert
        render.ShouldThrow<ArgumentException>();
    }
}
