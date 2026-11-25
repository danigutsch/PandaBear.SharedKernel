using SharedKernel.Testing.Http;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests the hypermedia link assertions on parsed JSON payloads.</summary>
public sealed class HypermediaLinkAssertionTests
{
    [Fact]
    public void Matching_read_link_passes()
    {
        // Arrange
        var payload = JsonPayload.Parse("""{"links":[{"rel":"self","href":"/items/42","method":"GET"}]}""");

        // Act, Assert
        payload.ShouldHaveGetLink("self", "/items/42");
    }

    [Theory]
    [InlineData("""{"links":[{"rel":"other","href":"/items/42","method":"GET"}]}""")]
    [InlineData("""{"links":[{"rel":"self","href":"/items/7","method":"GET"}]}""")]
    [InlineData("""{"links":[{"rel":"self","href":"/items/42","method":"POST"}]}""")]
    [InlineData("""{"links":[{"rel":"self","href":"/items/42","method":42}]}""")]
    [InlineData("""{"links":[{"rel":"self","href":"/items/42"}]}""")]
    public void Missing_or_mismatched_read_link_fails(string json)
    {
        // Arrange
        var payload = JsonPayload.Parse(json);

        // Act
        var failed = JsonPayload.Fails(() => payload.ShouldHaveGetLink("self", "/items/42"));

        // Assert
        failed.ShouldBeTrue();
    }

    [Fact]
    public void A_read_link_assertion_includes_the_reason_on_failure()
    {
        // Arrange
        const string reason = "The created item's self link must be readable.";
        var payload = JsonPayload.Parse("""{"links":[{"rel":"self","href":"/items/42","method":"POST"}]}""");

        // Act
        var message = JsonPayload.FailureMessage(() => payload.ShouldHaveGetLink("self", "/items/42", reason));

        // Assert
        message.ShouldStartWith(reason, StringComparison.Ordinal);
    }

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
