using PandaBear.Results;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the Should assertions that check whether a <see cref="Result" /> succeeded or failed with an expected status and code.</summary>
public sealed class ResultShouldAssertionTests
{
    [Fact]
    public void ShouldBeSuccess_does_not_format_the_successful_value()
    {
        // Arrange
        var payload = new UnformattableResultPayload();
        var result = Result.Ok(payload);

        // Act
        var value = result.ShouldBeSuccess();

        // Assert
        value.ShouldBeSameAs(payload);
    }

    [Fact]
    public void ShouldBeFailure_reports_the_outcome_without_formatting_the_value()
    {
        // Arrange
        var result = Result.Ok(new UnformattableResultPayload());
        Action assertion = () => result.ShouldBeFailure(ResultStatus.NotFound, ResultErrorCodes.NotFound);

        // Act, Assert
        assertion.ShouldThrow<Xunit.Sdk.TrueException>();
    }

    [Fact]
    public void ShouldBeSuccess_returns_the_generic_value()
    {
        // Arrange
        var result = Result.Created("created");

        // Act
        var value = result.ShouldBeSuccess();

        // Assert
        value.ShouldBe("created");
    }

    [Fact]
    public void ShouldBeSuccess_accepts_all_non_generic_success_statuses()
    {
        // Act, Assert
        Result.Ok().ShouldBeSuccess();
        Result.NoContent().ShouldBeSuccess();
        Result.Accepted().ShouldBeSuccess();
    }

    [Fact]
    public void Assertions_reject_uninitialized_results()
    {
        // Arrange
        Result result = default;
        Result<string> generic = default;
        Action success = () => result.ShouldBeSuccess();
        Action failure = () => result.ShouldBeFailure(ResultStatus.Invalid, "invalid");
        Action genericSuccess = () => generic.ShouldBeSuccess();
        Action genericFailure = () => generic.ShouldBeFailure(ResultStatus.Invalid, "invalid");

        // Act, Assert
        success.ShouldThrow<Xunit.Sdk.TrueException>();
        failure.ShouldThrow<Xunit.Sdk.TrueException>();
        genericSuccess.ShouldThrow<Xunit.Sdk.TrueException>();
        genericFailure.ShouldThrow<Xunit.Sdk.TrueException>();
    }

    [Fact]
    public void ShouldBeFailure_checks_status_code_and_validation_messages()
    {
        // Arrange
        var result = Result.Invalid<string>("Invalid id", "Id", "Id is required");

        // Act
        var error = result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);

        // Assert
        error.Detail.ShouldBe("Invalid id");
        error.ShouldHaveValidationError("Id", "Id is required");
        Action wrongCode = () => result.ShouldBeFailure(ResultStatus.Invalid, "different");
        Action wrongStatus = () => result.ShouldBeFailure(ResultStatus.NotFound, ResultErrorCodes.Invalid);
        Action wrongMessage = () => error.ShouldHaveValidationError("Id", "wrong message");
        wrongCode.ShouldThrow<Xunit.Sdk.EqualException>();
        wrongStatus.ShouldThrow<Xunit.Sdk.EqualException>();
        wrongMessage.ShouldThrow<Xunit.Sdk.EqualException>();
    }

    [Fact]
    public void ShouldHaveValidationError_accepts_the_nullable_error_details_of_a_result()
    {
        // Arrange
        var invalid = Result.Invalid("Invalid id", "Id", "Id is required");
        var success = Result.Ok();
        Action missingError = () => success.ErrorDetails.ShouldHaveValidationError("Id", "Id is required");

        // Act
        invalid.ErrorDetails.ShouldHaveValidationError("Id", "Id is required");

        // Assert
        missingError.ShouldThrow<Xunit.Sdk.NotNullException>();
    }

    [Fact]
    public void Success_and_failure_assertions_reject_the_opposite_outcome()
    {
        // Arrange
        var failure = Result.NotFound("missing");
        var success = Result.Ok("value");
        Action expectSuccess = () => failure.ShouldBeSuccess();
        Action expectFailure = () => success.ShouldBeFailure(ResultStatus.NotFound, ResultErrorCodes.NotFound);

        // Act, Assert
        expectSuccess.ShouldThrow<Xunit.Sdk.TrueException>();
        expectFailure.ShouldThrow<Xunit.Sdk.TrueException>();
    }
}
