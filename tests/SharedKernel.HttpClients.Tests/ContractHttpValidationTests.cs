using System.Net;
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
                """)
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
            Content = new StringContent("{ not json")
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
    public async Task Ensure_success_preserves_not_supported_exception_for_non_json_validation_problem()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new ContractHttpValidationNotSupportedContent()
        };

        var exception = await ((Func<Task>)(() => ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken))).ShouldThrow<ContractValidationException>();
        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.InnerException.ShouldBeOfType<NotSupportedException>();
        exception.ValidationErrors.ShouldBeEmpty();
        exception.Message.ShouldBe("Validation problem response body was not JSON.");
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
                """)
        };

        var exception = await ((Func<Task>)(() => ContractHttpValidation.EnsureSuccessOrThrowValidationException(
            response,
            ContractHttpValidationTestsJsonContext.Default.ContractValidationProblemDto,
            TestContext.Current.CancellationToken))).ShouldThrow<ContractValidationException>();
        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.ValidationErrors.ShouldBeEmpty();
        exception.Message.ShouldBe("Validation problem response body did not contain errors.");
    }

    [Fact]
    public async Task FromResponse_returns_generic_malformed_body_message_for_invalid_validation_problem_json()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("not json alice@example.test")
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
