using PandaBear.Results;

namespace PandaBear.Testing.Tests;

public sealed class ResultShouldAssertionTests
{
    [Fact]
    public void ShouldBeSuccess_does_not_format_the_successful_value()
    {
        var payload = new UnformattableResultPayload();
        var result = Result.Ok(payload);

        var value = result.ShouldBeSuccess();

        value.ShouldBeSameAs(payload);
    }

    [Fact]
    public void ShouldBeFailure_reports_the_outcome_without_formatting_the_value()
    {
        var result = Result.Ok(new UnformattableResultPayload());
        Action assertion = () => result.ShouldBeFailure(ResultStatus.NotFound, ResultErrorCodes.NotFound);

        assertion.ShouldThrow<Xunit.Sdk.TrueException>();
    }

    [Fact]
    public void ShouldBeSuccess_returns_the_generic_value()
    {
        var result = Result.Created("created");
        var value = result.ShouldBeSuccess();
        value.ShouldBe("created");
    }

    [Fact]
    public void ShouldBeSuccess_accepts_all_non_generic_success_statuses()
    {
        Result.Ok().ShouldBeSuccess();
        Result.NoContent().ShouldBeSuccess();
        Result.Accepted().ShouldBeSuccess();
    }

    [Fact]
    public void Assertions_reject_uninitialized_results()
    {
        Result result = default;
        Result<string> generic = default;
        Action success = () => result.ShouldBeSuccess();
        Action failure = () => result.ShouldBeFailure(ResultStatus.Invalid, "invalid");
        Action genericSuccess = () => generic.ShouldBeSuccess();
        Action genericFailure = () => generic.ShouldBeFailure(ResultStatus.Invalid, "invalid");
        success.ShouldThrow<Xunit.Sdk.TrueException>();
        failure.ShouldThrow<Xunit.Sdk.TrueException>();
        genericSuccess.ShouldThrow<Xunit.Sdk.TrueException>();
        genericFailure.ShouldThrow<Xunit.Sdk.TrueException>();
    }

    [Fact]
    public void ShouldBeFailure_checks_status_code_and_validation_messages()
    {
        var result = Result.Invalid<string>("Invalid id", "Id", "Id is required");
        var error = result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
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
    public void Success_and_failure_assertions_reject_the_opposite_outcome()
    {
        var failure = Result.NotFound("missing");
        var success = Result.Ok("value");
        Action expectSuccess = () => failure.ShouldBeSuccess();
        Action expectFailure = () => success.ShouldBeFailure(ResultStatus.NotFound, ResultErrorCodes.NotFound);
        expectSuccess.ShouldThrow<Xunit.Sdk.TrueException>();
        expectFailure.ShouldThrow<Xunit.Sdk.TrueException>();
    }
}
