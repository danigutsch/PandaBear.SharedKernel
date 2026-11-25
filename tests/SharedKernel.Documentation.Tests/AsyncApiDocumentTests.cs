using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandaBear.Documentation.Tests;

/// <summary>Verifies deterministic AsyncAPI rendering and artifact checks.</summary>
[Trait(Testing.TestTraitNames.CapabilityName, TestTraits.DocumentationGenerationCapability)]
[Trait(Testing.TestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class AsyncApiDocumentTests
{
    [Fact]
    public void Renders_shared_channels_and_payloads_from_source_generated_contracts()
    {
        // Arrange
        const string channel = "venue/~notices";
        var custom = new JsonObject { ["x-venue"] = new JsonObject { ["organizer"] = "Riverbank Hall" } };
        var opening = new AsyncApiMessage(channel, "OpeningV1", "VenueOpeningV1", "opening", "Hall opens",
            VenueNoticeJsonContext.Default.VenueOpeningNotice)
        {
            Send = new AsyncApiOperation("announceOpening", "The front desk publishes the new opening time."),
            Extensions = custom,
        };
        var closing = new AsyncApiMessage(channel, "ClosingV1", "VenueClosingV1", "closing", "Hall closes",
            VenueNoticeJsonContext.Default.VenueClosingNotice)
        {
            Receive = new AsyncApiOperation("showClosing", "The schedule displays the closing time."),
        };

        // Act
        var generated = AsyncApiDocument.Render("Venue notices", "2.0.0", "Public opening hours.", [opening, closing], extensions: custom);
        var reversed = AsyncApiDocument.Render("Venue notices", "2.0.0", "Public opening hours.", [closing, opening], extensions: custom);
        using var json = JsonDocument.Parse(generated);
        var document = json.RootElement;

        // Assert
        generated.ShouldBe(reversed);
        generated.ShouldEndWith("\n", StringComparison.Ordinal);
        generated.ShouldNotContain("\r", StringComparison.Ordinal);
        document.GetProperty("asyncapi").GetString().ShouldBe("3.0.0");
        var channelMessages = document.GetProperty("channels").GetProperty(channel).GetProperty("messages");
        channelMessages.EnumerateObject().Select(property => property.Name).ShouldBe(["closingV1", "openingV1"]);
        channelMessages.GetProperty("openingV1").GetProperty("$ref").GetString().ShouldBe("#/components/messages/OpeningV1");
        var operation = document.GetProperty("operations").GetProperty("announceOpening");
        operation.GetProperty("action").GetString().ShouldBe("send");
        operation.GetProperty("channel").GetProperty("$ref").GetString().ShouldBe("#/channels/venue~1~0notices");
        operation.GetProperty("messages")[0].GetProperty("$ref").GetString()
            .ShouldBe("#/channels/venue~1~0notices/messages/openingV1");
        var components = document.GetProperty("components");
        var message = components.GetProperty("messages").GetProperty("OpeningV1");
        message.GetProperty("x-venue").GetProperty("organizer").GetString().ShouldBe("Riverbank Hall");
        document.GetProperty("x-venue").GetProperty("organizer").GetString().ShouldBe("Riverbank Hall");
        var schemas = components.GetProperty("schemas");
        var payload = schemas.GetProperty("VenueOpeningV1");
        payload.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldBe(["venueRoom", "doorsOpenAt", "poster"]);
        var nested = schemas.GetProperty("VenueClosingV1").GetProperty("properties").GetProperty("nextNotice");
        nested.GetRawText().ShouldContain("\"$ref\": \"#/components/schemas/VenueClosingV1", StringComparison.Ordinal);
    }

    [Fact]
    public void Drift_check_accepts_current_text_and_rejects_missing_or_changed_text()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        var generated = AsyncApiDocument.Render("Venue notices", "1.0.0", "Public opening hours.", []);
        var file = Path.Combine(workspace.RootPath, "asyncapi.json");

        // Act
        var missing = AsyncApiDocument.MatchesFile(file, generated);
        workspace.WriteFile("asyncapi.json", generated);
        var current = AsyncApiDocument.MatchesFile(file, generated);
        workspace.WriteFile("asyncapi.json", generated.TrimEnd());
        var stale = AsyncApiDocument.MatchesFile(file, generated);

        // Assert
        missing.ShouldBeFalse();
        current.ShouldBeTrue();
        stale.ShouldBeFalse();
    }
}
