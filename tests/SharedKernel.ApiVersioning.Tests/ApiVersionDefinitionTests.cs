namespace SharedKernel.ApiVersioning.Tests;

/// <summary>
/// Verifies API version metadata behavior.
/// </summary>
public sealed class ApiVersionDefinitionTests
{
    [Fact]
    public void Uses_route_segment_as_default_openapi_document_name()
    {
        // Arrange
        var definition = new ApiVersionDefinition(new ApiVersion(1));

        // Act
        string documentName = definition.OpenApiDocumentName;

        // Assert
        documentName.ShouldBe("v1");
    }

    [Fact]
    public void Keeps_deprecation_metadata_with_version_definition()
    {
        // Arrange
        var deprecatedOn = new DateOnly(2026, 1, 1);
        var sunsetOn = new DateOnly(2026, 12, 31);
        var policy = new ApiDeprecationPolicy(deprecatedOn, sunsetOn, new Uri("https://example.test/api-migration"));

        // Act
        var definition = new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Deprecated, policy);

        // Assert
        definition.Status.ShouldBe(ApiVersionStatus.Deprecated);
        definition.Deprecation.ShouldBe(policy);
    }
}
