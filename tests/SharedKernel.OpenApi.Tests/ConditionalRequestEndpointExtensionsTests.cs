using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Xunit;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Verifies that conditional-request endpoints document the required <c>If-Match</c> header with their 400, 412 and 428 problems, and that
/// reads document the <c>ETag</c> they return.
/// </summary>
public sealed class ConditionalRequestEndpointExtensionsTests
{
    [Fact]
    public async Task A_conditional_write_documents_a_required_if_match_header_and_its_problems()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocument("settings", group =>
            group.MapPut("/item", () => TypedResults.Ok()).WithIfMatchPrecondition());

        // Act
        var operation = document.Paths["/settings/item"].Operations.ShouldNotBeNull()[HttpMethod.Put];

        // Assert
        var header = operation.Parameters.ShouldNotBeNull().Where(parameter => parameter.Name == "If-Match").ShouldHaveSingleItem();
        header.In.ShouldBe(ParameterLocation.Header);
        header.Required.ShouldBeTrue();
        header.Description.ShouldBe(ConditionalRequestEndpointExtensions.IfMatchDescription);
        operation.Responses.ShouldNotBeNull().Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["200", "400", "412", "428"]);
        operation.Responses["412"].Content.ShouldNotBeNull().Keys.ShouldContain("application/problem+json");
    }

    [Fact]
    public async Task The_if_match_description_can_be_replaced()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocument("settings", group =>
            group.MapDelete("/item", () => TypedResults.NoContent()).WithIfMatchPrecondition("The tag of the latest GET."));

        // Act
        var operation = document.Paths["/settings/item"].Operations.ShouldNotBeNull()[HttpMethod.Delete];

        // Assert
        operation.Parameters.ShouldNotBeNull().Where(parameter => parameter.Name == "If-Match").ShouldHaveSingleItem().Description.ShouldBe("The tag of the latest GET.");
    }

    [Fact]
    public async Task A_read_documents_the_entity_tag_header_of_its_success_response()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocument("settings", group =>
            group.MapGet("/item", () => TypedResults.Ok("value")).WithEntityTagResponseHeader());

        // Act
        var operation = document.Paths["/settings/item"].Operations.ShouldNotBeNull()[HttpMethod.Get];

        // Assert
        var header = operation.Responses.ShouldNotBeNull()["200"].Headers.ShouldNotBeNull()["ETag"];
        header.Required.ShouldBeTrue();
        header.Description.ShouldBe(ConditionalRequestEndpointExtensions.EntityTagDescription);
    }
}
