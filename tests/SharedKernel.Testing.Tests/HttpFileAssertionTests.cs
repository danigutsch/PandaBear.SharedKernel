using System.Net;
using PandaBear.Testing.Http;
using Xunit.Sdk;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the file download assertion.</summary>
public sealed class HttpFileAssertionTests
{
    [Theory]
    [InlineData("application/pdf", "attachment; filename=\"report.pdf\"")]
    [InlineData("Application/PDF; charset=binary", "attachment; filename=report.pdf")]
    [InlineData("application/pdf", "ATTACHMENT; FileName=\"report.pdf\"")]
    public async Task File_returns_the_bytes_when_media_type_disposition_and_name_match(string mediaType, string disposition)
    {
        // Arrange
        using var response = SuccessResponses.File(mediaType, disposition, 1, 2, 3);

        // Act
        var bytes = await response.ShouldBeFile("application/pdf", "report.pdf", SuccessResponses.Token);

        // Assert
        bytes.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task File_without_an_expected_name_checks_only_the_attachment()
    {
        // Arrange
        using var response = SuccessResponses.File("text/csv", "attachment", 9);

        // Act
        var bytes = await response.ShouldBeFile("text/csv", cancellationToken: SuccessResponses.Token);

        // Assert
        bytes.ShouldBe([9]);
    }

    [Fact]
    public async Task File_matches_the_media_type_ignoring_parameter_and_case_of_the_expectation_too()
    {
        // Arrange
        using var response = SuccessResponses.File("text/csv; charset=utf-8", "attachment; filename=a.csv", 9);

        // Act
        var bytes = await response.ShouldBeFile("TEXT/CSV; charset=ascii", "a.csv", SuccessResponses.Token);

        // Assert
        bytes.ShouldBe([9]);
    }

    [Fact]
    public async Task File_prefers_the_rfc_5987_extended_name_over_the_plain_name()
    {
        // Arrange
        using var response = SuccessResponses.File(
            "application/pdf", "attachment; filename=\"naive report.pdf\"; filename*=UTF-8''na%C3%AFve%20report.pdf", 1);
        Func<Task> plainName = async () => await response.ShouldBeFile("application/pdf", "naive report.pdf", SuccessResponses.Token);

        // Act
        var bytes = await response.ShouldBeFile("application/pdf", "naïve report.pdf", SuccessResponses.Token);
        var failure = await plainName.ShouldThrow<XunitException>();

        // Assert
        bytes.ShouldBe([1]);
        failure.Message.ShouldBe("Expected file name 'naive report.pdf' but was 'naïve report.pdf'.");
    }

    [Fact]
    public async Task File_reads_an_extended_name_without_a_plain_name()
    {
        // Arrange
        using var response = SuccessResponses.File("application/pdf", "attachment; filename*=UTF-8''r%C3%A9sum%C3%A9.pdf", 1);

        // Act
        var bytes = await response.ShouldBeFile("application/pdf", "résumé.pdf", SuccessResponses.Token);

        // Assert
        bytes.ShouldBe([1]);
    }

    [Fact]
    public async Task File_with_another_name_fails()
    {
        // Arrange
        using var response = SuccessResponses.File("application/pdf", "attachment; filename=\"other.pdf\"", 1);
        Func<Task> assertion = async () => await response.ShouldBeFile("application/pdf", "report.pdf", SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected file name 'report.pdf' but was 'other.pdf'.");
    }

    [Fact]
    public async Task File_with_a_disposition_that_has_no_name_fails_when_a_name_is_expected()
    {
        // Arrange
        using var response = SuccessResponses.File("application/pdf", "attachment", 1);
        Func<Task> assertion = async () => await response.ShouldBeFile("application/pdf", "report.pdf", SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected file name 'report.pdf' but the Content-Disposition has none.");
    }

    [Fact]
    public async Task File_without_a_disposition_or_with_inline_fails()
    {
        // Arrange
        using var missing = SuccessResponses.File("application/pdf", null, 1);
        using var inline = SuccessResponses.File("application/pdf", "inline; filename=report.pdf", 1);
        Func<Task> missingAssertion = async () => await missing.ShouldBeFile("application/pdf", cancellationToken: SuccessResponses.Token);
        Func<Task> inlineAssertion = async () => await inline.ShouldBeFile("application/pdf", cancellationToken: SuccessResponses.Token);

        // Act
        var missingFailure = await missingAssertion.ShouldThrow<XunitException>();
        var inlineFailure = await inlineAssertion.ShouldThrow<XunitException>();

        // Assert
        missingFailure.Message.ShouldBe("Expected Content-Disposition 'attachment' but the response has none.");
        inlineFailure.Message.ShouldBe("Expected Content-Disposition 'attachment' but was 'inline'.");
    }

    [Fact]
    public async Task File_with_another_media_type_fails_and_quotes_a_text_body()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, "oops", "text/plain");
        Func<Task> assertion = async () => await response.ShouldBeFile("application/pdf", cancellationToken: SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected content type 'application/pdf' but was 'text/plain'. Body: oops");
    }

    [Fact]
    public async Task File_with_another_status_fails()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.NotFound, """{"detail":"missing"}""");
        Func<Task> assertion = async () => await response.ShouldBeFile("application/pdf", cancellationToken: SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("""Expected HTTP status 200 (OK) but was 404 (NotFound). Body: {"detail":"missing"}""");
    }

    [Fact]
    public async Task File_validates_arguments_and_cancellation()
    {
        // Arrange
        HttpResponseMessage missing = null!;
        using var response = SuccessResponses.File("application/pdf", "attachment", 1);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> nullResponse = async () => await missing.ShouldBeFile("application/pdf", cancellationToken: SuccessResponses.Token);
        Func<Task> nullContentType = async () => await response.ShouldBeFile(null!, cancellationToken: SuccessResponses.Token);
        Func<Task> canceled = async () => await response.ShouldBeFile("application/pdf", cancellationToken: cancellation.Token);

        // Act
        await nullResponse.ShouldThrow<ArgumentNullException>();
        await nullContentType.ShouldThrow<ArgumentNullException>();
        var cancellationFailure = await canceled.ShouldThrow<OperationCanceledException>();

        // Assert
        cancellationFailure.CancellationToken.ShouldBe(cancellation.Token);
    }
}
