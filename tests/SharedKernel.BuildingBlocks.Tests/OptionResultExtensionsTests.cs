using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

public sealed class OptionResultExtensionsTests
{
    [Fact]
    public void Converts_a_present_option_to_an_ok_result()
    {
        // Arrange
        var entity = new TestIdentifiedEntity(Guid.Parse("5e4e8b2a-c825-4a43-89a8-e2697c8b45a6"));
        var option = Option.Some(entity);

        // Act
        var result = option.ToNotFoundResult(entity.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(entity);
    }

    [Fact]
    public void Converts_an_empty_option_to_a_standard_not_found_result()
    {
        // Arrange
        var entityId = Guid.Parse("5e4e8b2a-c825-4a43-89a8-e2697c8b45a6");
        var option = Option.None<TestIdentifiedEntity>();

        // Act
        var result = option.ToNotFoundResult(entityId);

        // Assert
        result.Status.ShouldBe(ResultStatus.NotFound);
        var error = result.ErrorDetails;
        error.ShouldNotBeNull();
        error.Detail.ShouldBe($"TestIdentifiedEntity with ID {entityId} not found.");
    }

    [Fact]
    public void Includes_the_container_in_an_empty_option_not_found_result()
    {
        // Arrange
        var entityId = Guid.Parse("5e4e8b2a-c825-4a43-89a8-e2697c8b45a6");
        var option = Option.None<TestIdentifiedEntity>();

        // Act
        var result = option.ToNotFoundResult(entityId, "this test");

        // Assert
        var error = result.ErrorDetails;
        error.ShouldNotBeNull();
        error.Detail.ShouldBe($"TestIdentifiedEntity with ID {entityId} not found in this test.");
    }
}
