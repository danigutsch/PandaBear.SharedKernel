using System.Text.Json;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests HypermediaLink validation, equality, and wire shape.</summary>
public sealed class HypermediaLinkTests
{
    [Fact]
    public void Create_withvalidvalues_returnslink()
    {
        // Act
        var result = HypermediaLink.Create("self", "/items/1", "GET");

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNull();
        result.Value.Rel.ShouldBe("self");
        result.Value.Href.ShouldBe("/items/1");
        result.Value.Method.ShouldBe("GET");
    }

    [Theory]
    [InlineData(null, "/x", "GET", "rel")]
    [InlineData("", "/x", "GET", "rel")]
    [InlineData(" ", "/x", "GET", "rel")]
    [InlineData("self", null, "GET", "href")]
    [InlineData("self", "", "GET", "href")]
    [InlineData("self", " ", "GET", "href")]
    [InlineData("self", "/x", null, "method")]
    [InlineData("self", "/x", "", "method")]
    [InlineData("self", "/x", " ", "method")]
    public void Create_withblankvalue_returnsinvalidresultnamingthefield(string? rel, string? href, string? method, string field)
    {
        // Act
        var result = HypermediaLink.Create(rel, href, method);

        // Assert
        var error = result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        error.ShouldHaveValidationError(field, $"The link {field} must not be blank.");
    }

    [Fact]
    public void Equals_withsamevalues_istrue()
    {
        // Arrange
        var left = HypermediaLink.Create("self", "/x", "GET").Value;
        var right = HypermediaLink.Create("self", "/x", "GET").Value;

        // Assert
        left.ShouldBe(right);
    }

    [Fact]
    public void Serialize_producesrelhrefmethodwireshape()
    {
        // Arrange
        var link = HypermediaLink.Create("self", "/x", "GET").Value!;

        // Act
        var json = JsonSerializer.Serialize(link, HypermediaLinkTestsJsonContext.Default.HypermediaLink);

        // Assert
        json.ShouldBe("""{"rel":"self","href":"/x","method":"GET"}""");
    }

    [Fact]
    public void Deserialize_roundtripsthewireshape()
    {
        // Act
        var link = JsonSerializer.Deserialize("""{"rel":"self","href":"/x","method":"GET"}""", HypermediaLinkTestsJsonContext.Default.HypermediaLink);

        // Assert
        link.ShouldBe(HypermediaLink.Create("self", "/x", "GET").Value);
    }

    [Fact]
    public void Deserialize_withblankvalue_throwsargumentexception()
    {
        // Arrange
        Func<object?> act = () => JsonSerializer.Deserialize("""{"rel":"","href":"/x","method":"GET"}""", HypermediaLinkTestsJsonContext.Default.HypermediaLink);

        // Act, Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Deserialize_withmissingvalue_throwsargumentnullexception()
    {
        // Arrange
        Func<object?> act = () => JsonSerializer.Deserialize("""{"rel":"self","href":"/x"}""", HypermediaLinkTestsJsonContext.Default.HypermediaLink);

        // Act, Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
