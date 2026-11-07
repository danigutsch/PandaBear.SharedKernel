namespace PandaBear.Results.Tests;

/// <summary>Tests reading validation errors from results and dropping result values.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ResultValidationExtensionsTests
{
    [Fact]
    public void ValidationErrors_returns_the_messages_by_field()
    {
        // Arrange
        var result = Result.Invalid("bad input", "email", "Email is required.");

        // Act
        var errors = result.ValidationErrors();

        // Assert
        errors.Keys.ToArray().ShouldBe(["email"]);
        errors["email"].ShouldBe(["Email is required."]);
    }

    [Fact]
    public void ValidationErrors_is_empty_for_a_success_and_for_a_failure_without_field_errors()
    {
        // Act
        var success = Result.Ok().ValidationErrors();
        var failure = Result.NotFound("missing").ValidationErrors();

        // Assert
        success.Count.ShouldBe(0);
        failure.Count.ShouldBe(0);
    }

    [Fact]
    public void WithoutValue_keeps_a_success()
    {
        // Act
        var result = Result.Ok(42).WithoutValue();

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void WithoutValue_keeps_the_failure_and_its_status()
    {
        // Act
        var result = Result.Invalid<int>("bad input", "age", "Age is invalid.").WithoutValue();

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ValidationErrors()["age"].ShouldBe(["Age is invalid."]);
    }
}
