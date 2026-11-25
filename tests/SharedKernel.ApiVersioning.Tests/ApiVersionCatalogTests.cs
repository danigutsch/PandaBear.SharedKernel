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
    public void Stores_versions_in_descending_order()
    {
        // Arrange
        var catalog = new ApiVersionCatalog(
        [
            new ApiVersionDefinition(new ApiVersion(1)),
            new ApiVersionDefinition(new ApiVersion(2, 1)),
            new ApiVersionDefinition(new ApiVersion(2))
        ]);

        // Act
        ApiVersion[] versions = [.. catalog.Versions.Select(static item => item.Version)];

        // Assert
        versions.ShouldBe([new ApiVersion(2, 1), new ApiVersion(2), new ApiVersion(1)]);
    }

    [Fact]
    public void Selectable_versions_exclude_retired_versions()
    {
        // Arrange
        var catalog = new ApiVersionCatalog(
        [
            new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Deprecated),
            new ApiVersionDefinition(new ApiVersion(2), ApiVersionStatus.Active),
            new ApiVersionDefinition(new ApiVersion(3), ApiVersionStatus.Retired)
        ]);

        // Act
        ApiVersion[] versions = [.. catalog.SelectableVersions.Select(static item => item.Version)];

        // Assert
        versions.ShouldBe([new ApiVersion(2), new ApiVersion(1)]);
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

    [Fact]
    public void Rejects_missing_versions()
    {
        // Act
        Action action = () => _ = new ApiVersionCatalog([]);

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Rejects_duplicate_versions()
    {
        // Arrange
        ApiVersionDefinition[] versions =
        [
            new ApiVersionDefinition(new ApiVersion(1)),
            new ApiVersionDefinition(new ApiVersion(1))
        ];

        // Act
        Action action = () => _ = new ApiVersionCatalog(versions);

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Rejects_null_version_definitions()
    {
        // Arrange
        ApiVersionDefinition[] versions = [new ApiVersionDefinition(new ApiVersion(1)), null!];

        // Act
        Action action = () => _ = new ApiVersionCatalog(versions);

        // Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Rejects_unknown_requested_version()
    {
        // Arrange
        var catalog = new ApiVersionCatalog([new ApiVersionDefinition(new ApiVersion(1))]);

        // Act
        Action action = () => catalog.Select(new ApiVersion(2));

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Rejects_default_selection_when_all_versions_are_retired()
    {
        // Arrange
        var catalog = new ApiVersionCatalog([new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Retired)]);

        // Act
        Action action = () => _ = catalog.Select();

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }
}
