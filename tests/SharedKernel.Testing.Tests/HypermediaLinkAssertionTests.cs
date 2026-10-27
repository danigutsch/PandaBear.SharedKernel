using PandaBear.Testing.Http;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the hypermedia link assertions on parsed JSON payloads.</summary>
public sealed class HypermediaLinkAssertionTests
{
    [Fact]
    public void Object_with_the_relation_returns_the_link()
    {
        // Arrange
        var payload = JsonPayload.Parse("""{"id":1,"links":[{"rel":"self","href":"/x/1","method":"GET"},{"rel":"edit","href":"/x/1","method":"PUT"}]}""");

        // Act
        var link = payload.ShouldHaveLink("edit");

        // Assert
        link.GetProperty("method").GetString().ShouldBe("PUT");
    }

    [Theory]
    [InlineData("""{"links":[{"rel":"self","href":"/x/1","method":"GET"}]}""")]
    [InlineData("""{"id":1}""")]
    [InlineData("""{"links":{}}""")]
    [InlineData("""{"links":[{"rel":"self"}]}""")]
    [InlineData("""{"links":[{"rel":"self","href":""}]}""")]
    [InlineData("""{"links":[{"rel":"self","href":5}]}""")]
    public void Missing_or_malformed_links_fail(string json)
    {
        // Arrange
        var payload = JsonPayload.Parse(json);

        // Act
        var failed = JsonPayload.Fails(() => payload.ShouldHaveLink("edit"));

        // Assert
        failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("""{"id":1}""")]
    [InlineData("""{"links":{}}""")]
    [InlineData("""{"links":[{"rel":"self"}]}""")]
    [InlineData("""{"links":[{"rel":"self","href":""}]}""")]
    [InlineData("""{"links":[{"rel":"self","href":5}]}""")]
    public void Malformed_links_fail_for_the_present_relation(string json)
    {
        // Arrange
        var payload = JsonPayload.Parse(json);

        // Act
        var failed = JsonPayload.Fails(() => payload.ShouldHaveLink("self"));

        // Assert
        failed.ShouldBeTrue();
    }

    [Fact]
    public void Collection_requires_the_relation_on_every_item()
    {
        // Arrange
        var good = JsonPayload.Parse("""[{"links":[{"rel":"self","href":"/a"}]},{"links":[{"rel":"self","href":"/b"}]}]""");
        var bad = JsonPayload.Parse("""[{"links":[{"rel":"self","href":"/a"}]},{"links":[]}]""");

        // Act
        good.ShouldAllHaveLink("self");
        var failed = JsonPayload.Fails(() => bad.ShouldAllHaveLink("self"));

        // Assert
        failed.ShouldBeTrue();
    }

    [Fact]
    public void Empty_collection_fails()
    {
        // Arrange
        var payload = JsonPayload.Parse("[]");

        // Act
        var failed = JsonPayload.Fails(() => payload.ShouldAllHaveLink("self"));

        // Assert
        failed.ShouldBeTrue();
    }

    [Fact]
    public void Payload_without_links_names_the_links_property_not_a_problem_property()
    {
        // Arrange
        var payload = JsonPayload.Parse("""{"id":1}""");

        // Act
        var message = JsonPayload.FailureMessage(() => payload.ShouldHaveLink("self"));

        // Assert
        message.ShouldNotBeNull().ShouldContain("payload property 'links'", StringComparison.Ordinal);
    }
}
