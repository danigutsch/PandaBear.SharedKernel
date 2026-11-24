using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Xunit;

namespace PandaBear.OpenApi.Tests;

/// <summary>Verifies Location header metadata in the generated API document.</summary>
public sealed class LocationResponseEndpointExtensionsTests
{
    [Fact]
    public async Task A_created_response_documents_a_required_location_URI_reference()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocument("items", group =>
            group.MapPost("/new", () => TypedResults.Created(new Uri("/items/1", UriKind.Relative), "new"))
                .WithLocationResponseHeader());

        // Act
        var response = document.Paths["/items/new"].Operations.ShouldNotBeNull()[HttpMethod.Post]
            .Responses.ShouldNotBeNull()["201"];

        // Assert
        var location = response.Headers.ShouldNotBeNull()["Location"];
        location.Required.ShouldBeTrue();
        location.Description.ShouldBe("URI reference of the created resource.");
        location.Schema.ShouldNotBeNull().Type.ShouldBe(JsonSchemaType.String);
        location.Schema.Format.ShouldBe("uri-reference");
        response.Content.ShouldNotBeNull().Keys.ShouldContain("application/json");
    }

    [Fact]
    public async Task Accepted_response_keeps_existing_headers_and_other_responses()
    {
        // Arrange
        const string description = "Relative URI to the operation status.";
        var document = await OpenApiDocumentFactory.CreateDocument("items", group =>
            group.MapDelete("/{id}", (int id) => TypedResults.Accepted(new Uri($"/items/{id}/status", UriKind.Relative)))
                .Produces(StatusCodes.Status404NotFound)
                .WithEntityTagResponseHeader(statusCode: StatusCodes.Status202Accepted)
                .WithLocationResponseHeader(description, StatusCodes.Status202Accepted));

        // Act
        var responses = document.Paths["/items/{id}"].Operations.ShouldNotBeNull()[HttpMethod.Delete]
            .Responses.ShouldNotBeNull();

        // Assert
        var headers = responses["202"].Headers.ShouldNotBeNull();
        headers["Location"].Description.ShouldBe(description);
        headers["Location"].Schema.ShouldNotBeNull().Format.ShouldBe("uri-reference");
        headers["ETag"].Required.ShouldBeTrue();
        string[] expectedStatuses = ["202", "404"];
        responses.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(expectedStatuses);
        var unmatchedResponseHasLocation = responses["404"].Headers?.ContainsKey("Location") ?? false;
        unmatchedResponseHasLocation.ShouldBeFalse();
    }

    [Fact]
    public async Task An_undocumented_response_is_not_added()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocument("items", group =>
            group.MapGet("/existing", () => TypedResults.Ok("item")).WithLocationResponseHeader());

        // Act
        var responses = document.Paths["/items/existing"].Operations.ShouldNotBeNull()[HttpMethod.Get]
            .Responses.ShouldNotBeNull();

        // Assert
        responses.Keys.ShouldHaveSingleItem().ShouldBe("200");
        var responseHasLocation = responses["200"].Headers?.ContainsKey("Location") ?? false;
        responseHasLocation.ShouldBeFalse();
    }
}
