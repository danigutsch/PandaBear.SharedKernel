using System.Net;
using PandaBear.Testing.Http;
using Xunit.Sdk;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the status assertions for success responses and the failure messages that quote the body.</summary>
public sealed class HttpStatusAssertionTests
{
    [Fact]
    public async Task Status_assertions_pass_and_return_the_response_for_chaining()
    {
        // Arrange
        using var ok = SuccessResponses.Empty(HttpStatusCode.OK);
        using var accepted = SuccessResponses.Empty(HttpStatusCode.Accepted);
        using var teapot = SuccessResponses.Empty(HttpStatusCode.Conflict);

        // Act
        var okResult = await ok.ShouldBeOk(SuccessResponses.Token);
        var acceptedResult = await accepted.ShouldBeAccepted(SuccessResponses.Token);
        var statusResult = await teapot.ShouldHaveStatus(HttpStatusCode.Conflict, SuccessResponses.Token);

        // Assert
        okResult.ShouldBeSameAs(ok);
        acceptedResult.ShouldBeSameAs(accepted);
        statusResult.ShouldBeSameAs(teapot);
    }

    [Fact]
    public async Task Wrong_status_names_both_statuses_and_quotes_the_body()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.NotFound, """{"detail":"missing"}""");
        Func<Task> assertion = async () => await response.ShouldBeOk(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("""Expected HTTP status 200 (OK) but was 404 (NotFound). Body: {"detail":"missing"}""");
    }

    [Theory]
    [InlineData(HttpStatusCode.Created, "Expected HTTP status 202 (Accepted) but was 201 (Created). Body: <empty>")]
    [InlineData(HttpStatusCode.OK, "Expected HTTP status 202 (Accepted) but was 200 (OK). Body: <empty>")]
    public async Task Accepted_rejects_other_statuses(HttpStatusCode status, string message)
    {
        // Arrange
        using var response = SuccessResponses.Empty(status);
        Func<Task> assertion = async () => await response.ShouldBeAccepted(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe(message);
    }

    [Fact]
    public async Task Long_text_bodies_are_truncated_in_the_failure_message()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.InternalServerError, new string('x', 500), "text/plain");
        Func<Task> assertion = async () => await response.ShouldBeOk(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe(
            "Expected HTTP status 200 (OK) but was 500 (InternalServerError). Body: " + new string('x', 200) + "... (truncated, 500 bytes in total)");
    }

    [Fact]
    public async Task Control_characters_in_the_body_are_flattened_to_spaces()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.BadRequest, "first\r\nsecond\tthird", "text/plain");
        Func<Task> assertion = async () => await response.ShouldBeOk(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected HTTP status 200 (OK) but was 400 (BadRequest). Body: first  second third");
    }

    [Fact]
    public async Task Binary_bodies_are_described_by_size_and_never_quoted()
    {
        // Arrange
        using var response = SuccessResponses.Bytes(HttpStatusCode.BadGateway, [1, 2, 3, 4, 5], "application/octet-stream");
        Func<Task> assertion = async () => await response.ShouldBeOk(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected HTTP status 200 (OK) but was 502 (BadGateway). Body: <5 bytes of application/octet-stream>");
    }

    [Fact]
    public async Task A_failed_assertion_leaves_the_body_readable()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.NotFound, """{"detail":"missing"}""");
        Func<Task> assertion = async () => await response.ShouldBeOk(SuccessResponses.Token);

        // Act
        await assertion.ShouldThrow<XunitException>();
        var body = await response.Content.ReadAsStringAsync(SuccessResponses.Token);

        // Assert
        body.ShouldBe("""{"detail":"missing"}""");
    }

    [Fact]
    public async Task No_content_requires_204_and_an_empty_body()
    {
        // Arrange
        using var noContent = SuccessResponses.Empty(HttpStatusCode.NoContent);
        using var wrongStatus = SuccessResponses.Text(HttpStatusCode.OK, """{"a":1}""");
        using var withBody = SuccessResponses.Text(HttpStatusCode.NoContent, """{"a":1}""");
        Func<Task> wrongStatusAssertion = async () => await wrongStatus.ShouldBeNoContent(SuccessResponses.Token);
        Func<Task> withBodyAssertion = async () => await withBody.ShouldBeNoContent(SuccessResponses.Token);

        // Act
        var result = await noContent.ShouldBeNoContent(SuccessResponses.Token);
        var statusFailure = await wrongStatusAssertion.ShouldThrow<XunitException>();
        var bodyFailure = await withBodyAssertion.ShouldThrow<XunitException>();

        // Assert
        result.ShouldBeSameAs(noContent);
        statusFailure.Message.ShouldBe("""Expected HTTP status 204 (NoContent) but was 200 (OK). Body: {"a":1}""");
        bodyFailure.Message.ShouldBe("""Expected an empty body but it was: {"a":1}""");
    }

    [Fact]
    public async Task Status_assertions_reject_a_null_response_and_a_canceled_token()
    {
        // Arrange
        HttpResponseMessage missing = null!;
        using var response = SuccessResponses.Empty(HttpStatusCode.OK);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> nullResponse = async () => await missing.ShouldBeOk(SuccessResponses.Token);
        Func<Task> canceled = async () => await response.ShouldBeOk(cancellation.Token);

        // Act
        await nullResponse.ShouldThrow<ArgumentNullException>();
        var cancellationFailure = await canceled.ShouldThrow<OperationCanceledException>();

        // Assert
        cancellationFailure.CancellationToken.ShouldBe(cancellation.Token);
    }
}
