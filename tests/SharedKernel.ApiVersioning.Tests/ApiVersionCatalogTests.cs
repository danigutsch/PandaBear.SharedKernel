namespace SharedKernel.ApiVersioning.Tests;

/// <summary>
/// Verifies API version selection behavior.
/// </summary>
public sealed class ApiVersionCatalogTests
{
    [Fact]
    public void Selects_latest_non_retired_version_by_default()
    {
        // Arrange
        var catalog = new ApiVersionCatalog(
        [
            new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Deprecated),
            new ApiVersionDefinition(new ApiVersion(2), ApiVersionStatus.Active),
            new ApiVersionDefinition(new ApiVersion(3), ApiVersionStatus.Retired)
        ]);

        // Act
        ApiVersionDefinition selected = catalog.Select();

        // Assert
        selected.Version.ShouldBe(new ApiVersion(2));
    }

    [Fact]
    public void Selects_requested_deprecated_version_when_it_is_still_available()
    {
        // Arrange
        var catalog = new ApiVersionCatalog(
        [
            new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Deprecated),
            new ApiVersionDefinition(new ApiVersion(2), ApiVersionStatus.Active)
        ]);

        // Act
        ApiVersionDefinition selected = catalog.Select(new ApiVersion(1));

        // Assert
        selected.Status.ShouldBe(ApiVersionStatus.Deprecated);
    }

    [Fact]
    public void Rejects_requested_retired_version()
    {
        // Arrange
        var catalog = new ApiVersionCatalog([new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Retired)]);

        // Act
        Action action = () => catalog.Select(new ApiVersion(1));

        // Assert
        action.ShouldThrow<ArgumentException>();
    }
}
