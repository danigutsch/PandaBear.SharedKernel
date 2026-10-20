using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SharedKernel.HttpClients.Tests;

public sealed class ContractHttpValidationTests
{
    [Fact]
    public async Task Ensure_success_returns_when_response_is_successful()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NoContent);

        await ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task Ensure_success_throws_http_exception_for_non_validation_failures()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        var exception = await ((Func<Task>)(() => ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken))).ShouldThrow<HttpRequestException>();
        exception.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ensure_success_throws_validation_exception_with_errors_for_validation_problem()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""
                {
                  "errors": {
                    "Name": ["Name is required.", "Name is too short."],
                    "Email": ["Email is invalid."]
                  }
                }
                """, Encoding.UTF8, "application/problem+json")
        };

        var exception = await ((Func<Task>)(() => ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken))).ShouldThrow<ContractValidationException>();
        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.ValidationErrors["Name"].ShouldBe(["Name is required.", "Name is too short."]);
        exception.ValidationErrors["Email"].ShouldBe(["Email is invalid."]);
        exception.Message.ShouldContain("Name is required.", StringComparison.Ordinal);
        exception.Message.ShouldContain("Email is invalid.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ensure_success_preserves_json_exception_for_malformed_validation_problem()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{ not json", Encoding.UTF8, "application/problem+json")
        };

        var exception = await ((Func<Task>)(() => ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken))).ShouldThrow<ContractValidationException>();
        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.InnerException.ShouldBeOfType<JsonException>();
        exception.ValidationErrors.ShouldBeEmpty();
        exception.Message.ShouldBe("Validation problem response body was malformed.");
    }

    [Fact]
    public async Task Ensure_success_rejects_unsupported_validation_content_without_reading_it()
    {
        var content = new ContractHttpValidationNotSupportedContent();
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = content
        };

        var exception = await ((Func<Task>)(() => ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken))).ShouldThrow<ContractValidationException>();
        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.InnerException.ShouldBeNull();
        exception.ValidationErrors.ShouldBeEmpty();
        exception.Message.ShouldBe("Validation problem response body was not JSON.");
        content.WasRead.ShouldBeFalse();
    }

    [Fact]
    public async Task Ensure_success_throws_validation_exception_when_validation_problem_has_no_errors()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""
                {
                  "title": "One or more validation errors occurred."
                }
                """, Encoding.UTF8, "application/problem+json")
        };

        var exception = await ((Func<Task>)(() => ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken))).ShouldThrow<ContractValidationException>();
        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.ValidationErrors.ShouldBeEmpty();
        exception.Message.ShouldBe("Validation problem response body did not contain errors.");
    }

    [Theory]
    [InlineData("{\"errors\":{\"Name\":null}}")]
    [InlineData("{\"errors\":{\"Name\":[]}}")]
    [InlineData("{\"errors\":{\" \":[\"Name is required.\"]}}")]
    [InlineData("{\"errors\":{\"Name\":[\" \"]}}")]
    [InlineData("{\"errors\":{\"Name\":[\"Valid message\",null]}}")]
    public async Task Ensure_success_rejects_invalid_validation_error_entries(string content)
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/problem+json")
        };

        // Act
        var exception = await ((Func<Task>)(() => ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken))).ShouldThrow<ContractValidationException>();

        // Assert
        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.ValidationErrors.ShouldBeEmpty();
        exception.Message.ShouldBe("Validation problem response body was malformed.");
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
    public async Task Ensure_success_bounds_oversized_validation_content()
    {
        // Arrange
        var oversizedContent = Encoding.UTF8.GetBytes(new string('x', 2 * 1024 * 1024));
        using var responseBody = new TrackingReadStream(oversizedContent);
        var content = new StreamContent(responseBody);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content };

        // Act
        var exception = await ((Func<Task>)(() => ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken))).ShouldThrow<ContractValidationException>();

        // Assert
        exception.Message.ShouldBe("Validation problem response body was malformed.");
        exception.ValidationErrors.ShouldBeEmpty();
        responseBody.BytesRead.ShouldBeLessThanOrEqualTo((1024 * 1024) + 1);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task FromResponse_returns_generic_malformed_body_message_for_invalid_validation_problem_json()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("not json alice@example.test", Encoding.UTF8, "application/problem+json")
        };

        var outcome = await ContractCommandOutcome.FromResponse(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.MalformedBody);
        outcome.Message.ShouldBe("Validation problem response body was malformed.");
        outcome.Message.ShouldNotBeNull();
        outcome.Message.Contains("alice@example.test", StringComparison.Ordinal).ShouldBeFalse();
    }
}
