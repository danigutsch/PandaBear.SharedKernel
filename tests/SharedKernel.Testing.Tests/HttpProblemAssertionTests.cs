using System.Net;
using System.Text;
using PandaBear.Testing.Http;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the HTTP problem-details assertions that check status, detail, code, and validation errors of a response body.</summary>
public sealed class HttpProblemAssertionTests
{
    [Fact]
    public async Task Validation_problem_checks_status_detail_code_and_exact_fields()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"status":400,"detail":"invalid id","code":"invalid","errors":{"Id":["required"]}}""", Encoding.UTF8, "application/problem+json")
        };

        // Act
        var problem = await response.ShouldBeValidationProblem("invalid id", "invalid", TestContext.Current.CancellationToken);

        // Assert
        problem.ShouldHaveValidationErrors(new Dictionary<string, string[]> { ["Id"] = ["required"] });
        Action wrongMessage = () => problem.ShouldHaveValidationError("Id", "other");
        wrongMessage.ShouldThrow<Xunit.Sdk.EqualException>();
        Action extraField = () => problem.ShouldHaveValidationErrors(new Dictionary<string, string[]>());
        extraField.ShouldThrow<Xunit.Sdk.EqualException>();
    }

    [Theory]
    [InlineData(200, "application/problem+json", "{\"status\":400,\"errors\":{\"Id\":[\"required\"]}}")]
    [InlineData(400, "application/json", "{\"status\":400,\"errors\":{\"Id\":[\"required\"]}}")]
    [InlineData(400, "application/problem+json", "{\"status\":422,\"errors\":{\"Id\":[\"required\"]}}")]
    public async Task Validation_problem_rejects_inconsistent_status_or_media_type(int status, string mediaType, string json)
    {
        // Arrange
        using var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(json, Encoding.UTF8, mediaType) };
        Func<Task> assertion = async () => await response.ShouldBeValidationProblem(cancellationToken: TestContext.Current.CancellationToken);

        // Act, Assert
        await assertion.ShouldThrow<Xunit.Sdk.EqualException>();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Id\":[]}")]
    [InlineData("{\"Id\":[\" \" ]}")]
    public async Task Validation_problem_rejects_empty_fields_or_messages(string errors)
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"status\":400,\"errors\":" + errors + "}", Encoding.UTF8, "application/problem+json")
        };
        Func<Task> assertion = async () => await response.ShouldBeValidationProblem(cancellationToken: TestContext.Current.CancellationToken);

        // Act, Assert
        await assertion.ShouldThrowAssignableTo<Xunit.Sdk.XunitException>();
    }

    [Fact]
    public async Task Problem_assertion_checks_code_and_preserves_response_ownership()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"status":404,"detail":"missing","code":"not_found"}""", Encoding.UTF8, "application/problem+json")
        };
        Func<Task> assertion = async () => await response.ShouldBeProblem(HttpStatusCode.NotFound, expectedCode: "wrong", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await assertion.ShouldThrow<Xunit.Sdk.EqualException>();
        var problem = await response.ShouldBeProblem(HttpStatusCode.NotFound, "missing", "not_found", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        problem.GetProperty("code").GetString().ShouldBe("not_found");
        body.ShouldContain("missing", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Problem_assertion_rejects_missing_code_and_cancelled_reads()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"status":400,"detail":"invalid id","errors":{"Id":["required"]}}""", Encoding.UTF8, "application/problem+json")
        };
        Func<Task> missingCode = async () => await response.ShouldBeValidationProblem(expectedCode: "invalid", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var failure = await missingCode.ShouldThrow<Xunit.Sdk.TrueException>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> cancelled = async () => await response.ShouldBeValidationProblem(cancellationToken: cancellation.Token);
        await cancelled.ShouldThrow<OperationCanceledException>();

        // Assert
        failure.Message.ShouldContain("code", StringComparison.Ordinal);
    }

}
