namespace PandaBear.Results.Tests;

/// <summary>
/// Tests collecting only the failed results of a validation step with ValidationErrors.AddIfFailed.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ValidationErrorsAddIfFailedTests
{
    [Fact]
    public void A_successful_result_adds_nothing()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.AddIfFailed(Result.Ok(42));
        errors.AddIfFailed(Result.Ok());

        // Assert
        errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void An_invalid_generic_result_is_collected_with_its_field()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.AddIfFailed(Result.Invalid<int>("Validation failed", "Age", "Age must be positive"));

        // Assert
        errors.HasErrors.ShouldBeTrue();
        errors.ToResult().ValidationErrors()["Age"].ShouldBe(["Age must be positive"]);
    }

    [Fact]
    public void An_invalid_result_is_collected_with_its_field()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.AddIfFailed(Result.Invalid("Validation failed", "Name", "Name is required"));

        // Assert
        errors.ToResult().ValidationErrors()["Name"].ShouldBe(["Name is required"]);
    }

    [Fact]
    public void Failures_of_several_steps_are_merged()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.AddIfFailed(Result.Invalid<int>("Validation failed", "Age", "Age must be positive"));
        errors.AddIfFailed(Result.Ok("fine"));
        errors.AddIfFailed(Result.Invalid<string>("Validation failed", "Name", "Name is required"));

        // Assert
        var fields = errors.ToResult().ValidationErrors();
        fields["Age"].ShouldBe(["Age must be positive"]);
        fields["Name"].ShouldBe(["Name is required"]);
        fields.Count.ShouldBe(2);
    }

    [Fact]
    public void A_failure_that_is_not_a_validation_error_is_rejected()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var act = () => errors.AddIfFailed(Result.NotFound<int>("Missing"));

        // Assert
        act.ShouldThrow<InvalidOperationException>();
        errors.HasErrors.ShouldBeFalse();
    }
}
