using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace PandaBear.Results.AspNet.Tests;

/// <summary>Tests the conversion of results to HTTP responses.</summary>
public sealed class ResultHttpExtensionsTests
{
    [Fact]
    public void A_successful_result_without_a_value_is_no_content()
    {
        // Act
        var http = Result.Ok().ToHttpResult();

        // Assert
        http.ShouldBeOfType<NoContent>();
    }

    [Fact]
    public void A_successful_result_with_a_value_is_ok_with_the_value_as_body()
    {
        // Act
        var http = Result.Ok("body").ToHttpResult();

        // Assert
        var ok = http.ShouldBeOfType<Ok<string>>();
        ok.Value.ShouldBe("body");
    }

    [Fact]
    public void An_invalid_result_is_a_validation_problem_with_its_field_errors()
    {
        // Act
        var http = Result.Invalid<string>("bad input", "email", "Email is required.").ToHttpResult();

        // Assert
        var problem = http.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        var details = problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        details.Errors["email"].ShouldBe(["Email is required."]);
    }

    [Fact]
    public void A_not_found_result_is_a_problem_with_the_mapped_status()
    {
        // Act
        var http = Result.NotFound("missing").ToHttpResult();

        // Assert
        var problem = http.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void An_uninitialized_result_throws_instead_of_becoming_a_response()
    {
        // Act
        var exception = ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Func<object?>)(() => default(Result).ToHttpResult()));

        // Assert
        exception.Message.ShouldBe("Result status is not initialized.");
    }

    [Fact]
    public void An_uninitialized_valued_result_throws_instead_of_becoming_a_response()
    {
        // Act
        var exception = ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Func<object?>)(() => default(Result<int>).ToHttpResult()));

        // Assert
        exception.Message.ShouldBe("Result status is not initialized.");
    }

    [Theory]
    [InlineData(ResultStatus.Error, StatusCodes.Status500InternalServerError, ResultHttpExtensions.InternalServerErrorDetail)]
    [InlineData(ResultStatus.CriticalError, StatusCodes.Status500InternalServerError, ResultHttpExtensions.InternalServerErrorDetail)]
    [InlineData(ResultStatus.Unavailable, StatusCodes.Status503ServiceUnavailable, ResultHttpExtensions.ServiceUnavailableDetail)]
    public void A_server_side_failure_never_sends_its_detail_to_the_client(ResultStatus status, int expectedStatusCode, string expectedDetail)
    {
        // Arrange
        var result = HttpResultTestSupport.FailureWithSecret(status);

        // Act
        var http = result.ToHttpResult();

        // Assert
        HttpResultTestSupport.AssertGenericProblem(http, expectedStatusCode, expectedDetail);
    }

    [Theory]
    [InlineData(ResultStatus.Error, StatusCodes.Status500InternalServerError, ResultHttpExtensions.InternalServerErrorDetail)]
    [InlineData(ResultStatus.CriticalError, StatusCodes.Status500InternalServerError, ResultHttpExtensions.InternalServerErrorDetail)]
    [InlineData(ResultStatus.Unavailable, StatusCodes.Status503ServiceUnavailable, ResultHttpExtensions.ServiceUnavailableDetail)]
    public void A_server_side_valued_failure_never_sends_its_detail_to_the_client(ResultStatus status, int expectedStatusCode, string expectedDetail)
    {
        // Arrange
        var result = HttpResultTestSupport.ValuedFailureWithSecret(status);

        // Act
        var http = result.ToHttpResult();

        // Assert
        HttpResultTestSupport.AssertGenericProblem(http, expectedStatusCode, expectedDetail);
    }

    [Theory]
    [InlineData(ResultStatus.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ResultStatus.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ResultStatus.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ResultStatus.Unauthorized, StatusCodes.Status401Unauthorized)]
    public void A_client_side_failure_keeps_its_detail(ResultStatus status, int expectedStatusCode)
    {
        // Arrange
        var result = status switch
        {
            ResultStatus.NotFound => Result.NotFound("the detail"),
            ResultStatus.Conflict => Result.Conflict("the detail"),
            ResultStatus.Forbidden => Result.Forbidden("the detail"),
            _ => Result.Unauthorized("the detail"),
        };

        // Act
        var http = result.ToHttpResult();

        // Assert
        var problem = http.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(expectedStatusCode);
        problem.ProblemDetails.Detail.ShouldBe("the detail");
    }

    [Fact]
    public void An_invalid_result_keeps_its_detail()
    {
        // Act
        var http = Result.Invalid("the detail", "email", "Email is required.").ToHttpResult();

        // Assert
        var problem = http.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Detail.ShouldBe("the detail");
    }

    [Fact]
    public void A_created_result_with_a_value_is_created_without_a_location()
    {
        // Act
        var http = Result.Created("body").ToHttpResult();

        // Assert
        var created = http.ShouldBeOfType<Created<string>>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);
        created.Value.ShouldBe("body");
        created.Location.ShouldBeNull();
    }

    [Fact]
    public void An_accepted_result_with_a_value_is_accepted_with_the_value_as_body()
    {
        // Act
        var http = Result.Accepted("body").ToHttpResult();

        // Assert
        var accepted = http.ShouldBeOfType<Accepted<string>>();
        accepted.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        accepted.Value.ShouldBe("body");
    }

    [Fact]
    public void An_accepted_result_without_a_value_is_accepted()
    {
        // Act
        var http = Result.Accepted().ToHttpResult();

        // Assert
        var accepted = http.ShouldBeOfType<Accepted>();
        accepted.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
    }

    [Fact]
    public void A_no_content_result_without_a_value_is_no_content()
    {
        // Act
        var http = Result.NoContent().ToHttpResult();

        // Assert
        http.ShouldBeOfType<NoContent>();
    }
}
