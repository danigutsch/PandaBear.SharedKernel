namespace SharedKernel.Results.Tests;

/// <summary>
/// Tests recording one message on a field with ValidationErrors.Add.
/// </summary>
[Trait(Testing.TestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(Testing.TestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ValidationErrorsAddMessageTests
{
    [Fact]
    public void A_message_is_recorded_on_its_field_and_is_the_detail()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.Add("name", "Name is required.");

        // Assert
        var result = errors.ToResult();
        result.ValidationErrors()["name"].ShouldBe(["Name is required."]);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("Name is required.");
    }

    [Fact]
    public void Messages_merge_with_added_results_in_order()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.Add("name", "Name is required.");
        errors.Add(Result.Invalid("Validation failed", "age", "Age must be positive."));
        errors.Add("name", "Name cannot exceed 10 characters.");

        // Assert
        var fields = errors.ToResult().ValidationErrors();
        fields["name"].ShouldBe(["Name is required.", "Name cannot exceed 10 characters."]);
        fields["age"].ShouldBe(["Age must be positive."]);
    }

    [Theory]
    [InlineData("", "Name is required.")]
    [InlineData(" ", "Name is required.")]
    [InlineData("name", "")]
    [InlineData("name", " ")]
    public void A_blank_field_or_message_is_rejected_and_records_nothing(string field, string message)
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var act = () => errors.Add(field, message);

        // Assert
        act.ShouldThrow<ArgumentException>();
        errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void A_null_field_or_message_is_rejected()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var nullField = () => errors.Add(null!, "Name is required.");
        var nullMessage = () => errors.Add("name", null!);

        // Assert
        nullField.ShouldThrow<ArgumentNullException>();
        nullMessage.ShouldThrow<ArgumentNullException>();
        errors.HasErrors.ShouldBeFalse();
    }
}
