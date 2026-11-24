using System.Text.Json.Nodes;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the JSON-LD script.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class JsonLdTests : BunitContext
{
    private const string Hostile = "</script><script>alert(1)</script><!--";

    [Fact]
    public void The_data_is_written_as_a_linked_data_script_of_JSON()
    {
        // Arrange
        var data = new JsonObject { ["@context"] = "https://schema.org", ["@type"] = "TouristTrip", ["name"] = "Atacama" };

        // Act
        var cut = Render<JsonLd>(parameters => parameters.Add(component => component.Data, data));

        // Assert
        var script = cut.Find("script");
        script.GetAttribute("type").ShouldBe("application/ld+json");
        script.Attributes.Length.ShouldBe(1);
        script.TextContent.ShouldBe("""{"@context":"https://schema.org","@type":"TouristTrip","name":"Atacama"}""");
    }

    [Fact]
    public void A_value_or_a_name_that_holds_a_closing_script_tag_is_escaped_so_it_cannot_close_the_script()
    {
        // Arrange
        var data = new JsonObject { ["name"] = Hostile, [Hostile] = new JsonArray("a & b", "'quoted'") };

        // Act
        var cut = Render<JsonLd>(parameters => parameters.Add(component => component.Data, data));

        // Assert
        var markup = cut.Markup;
        markup.IndexOf("</script", StringComparison.OrdinalIgnoreCase).ShouldBe(markup.LastIndexOf("</script", StringComparison.OrdinalIgnoreCase));
        markup.ShouldNotContain("<!--", StringComparison.Ordinal);
        markup.ShouldContain("\\u003C/script\\u003E", StringComparison.Ordinal);
        var parsed = JsonNode.Parse(cut.Find("script").TextContent).ShouldNotBeNull();
        parsed["name"]!.GetValue<string>().ShouldBe(Hostile);
        parsed[Hostile]!.AsArray().Select(item => item!.GetValue<string>()).ShouldBe(["a & b", "'quoted'"]);
    }

    [Fact]
    public void Text_outside_ascii_survives_the_escaping()
    {
        // Arrange
        var data = new JsonObject { ["name"] = "São Paulo – Atacama" };

        // Act
        var cut = Render<JsonLd>(parameters => parameters.Add(component => component.Data, data));

        // Assert
        JsonNode.Parse(cut.Find("script").TextContent)!["name"]!.GetValue<string>().ShouldBe("São Paulo – Atacama");
    }

    [Fact]
    public void An_array_of_items_is_written_too()
    {
        // Arrange
        var data = new JsonArray(new JsonObject { ["@type"] = "Organization" }, new JsonObject { ["@type"] = "WebSite" });

        // Act
        var cut = Render<JsonLd>(parameters => parameters.Add(component => component.Data, data));

        // Assert
        cut.Find("script").TextContent.ShouldBe("""[{"@type":"Organization"},{"@type":"WebSite"}]""");
    }

    [Fact]
    public void Missing_data_is_refused()
    {
        // Arrange & Act
        var render = () => Render<JsonLd>(parameters => parameters.Add(component => component.Data, null));

        // Assert
        render.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(JsonLd.Data));
    }

    [Fact]
    public void A_bare_value_is_refused_because_linked_data_is_an_object_or_an_array()
    {
        // Arrange & Act
        var render = () => Render<JsonLd>(parameters => parameters.Add(component => component.Data, JsonValue.Create("Atacama")));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(JsonLd.Data));
    }
}
