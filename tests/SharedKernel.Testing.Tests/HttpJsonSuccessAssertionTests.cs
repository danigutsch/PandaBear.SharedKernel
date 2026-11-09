using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PandaBear.Testing.Http;
using Xunit.Sdk;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the assertions that deserialize the JSON body of an OK or Created response.</summary>
public sealed class HttpJsonSuccessAssertionTests
{
    [Theory]
    [InlineData("""{"id":7,"startCity":"Lisbon"}""")]
    [InlineData("""{"ID":7,"STARTCITY":"Lisbon"}""")]
    public async Task Ok_reads_the_body_with_web_defaults(string json)
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, json);

        // Act
        var payload = await response.ShouldBeOk<TourPayload>(SuccessResponses.Token);

        // Assert
        payload.ShouldBe(new TourPayload(7, "Lisbon"));
    }

    [Fact]
    public async Task Ok_ignores_media_type_parameters_and_case()
    {
        // Arrange
        using var response = SuccessResponses.Bytes(
            HttpStatusCode.OK, """{"id":7,"startCity":"Lisbon"}"""u8.ToArray(), "Application/JSON; charset=utf-8");

        // Act
        var payload = await response.ShouldBeOk<TourPayload>(SuccessResponses.Token);

        // Assert
        payload.Id.ShouldBe(7);
    }

    [Fact]
    public async Task Ok_uses_the_supplied_serializer_options()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, """{"id":7,"start_city":"Lisbon"}""");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        // Act
        var payload = await response.ShouldBeOk<TourPayload>(options, SuccessResponses.Token);

        // Assert
        payload.ShouldBe(new TourPayload(7, "Lisbon"));
    }

    [Fact]
    public async Task Ok_uses_source_generated_metadata()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, """{"id":7,"startCity":"Lisbon"}""");

        // Act
        var payload = await response.ShouldBeOk(TourPayloadJsonContext.Default.TourPayload, SuccessResponses.Token);

        // Assert
        payload.ShouldBe(new TourPayload(7, "Lisbon"));
    }

    [Fact]
    public async Task Ok_with_a_body_type_checks_the_status_first_and_quotes_the_body()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.NotFound, """{"detail":"missing"}""", "application/problem+json");
        Func<Task> assertion = async () => await response.ShouldBeOk<TourPayload>(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("""Expected HTTP status 200 (OK) but was 404 (NotFound). Body: {"detail":"missing"}""");
    }

    [Fact]
    public async Task Ok_rejects_another_media_type_and_quotes_the_body()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, "hello", "text/plain");
        Func<Task> assertion = async () => await response.ShouldBeOk<TourPayload>(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected content type 'application/json' but was 'text/plain'. Body: hello");
    }

    [Fact]
    public async Task Ok_rejects_a_body_without_a_content_type()
    {
        // Arrange
        using var response = SuccessResponses.Bytes(HttpStatusCode.OK, [1, 2], null);
        Func<Task> assertion = async () => await response.ShouldBeOk<TourPayload>(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected content type 'application/json' but the response has none. Body: <2 bytes of unknown content type>");
    }

    [Fact]
    public async Task Ok_rejects_an_empty_body()
    {
        // Arrange
        using var response = SuccessResponses.Bytes(HttpStatusCode.OK, [], "application/json");
        Func<Task> assertion = async () => await response.ShouldBeOk<TourPayload>(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected a JSON body but it was empty.");
    }

    [Fact]
    public async Task Ok_reports_a_deserialization_failure_with_the_inner_exception_and_the_body()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, """{"id":"seven"}""");
        Func<Task> assertion = async () => await response.ShouldBeOk<TourPayload>(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        var inner = failure.InnerException.ShouldBeOfType<JsonException>();
        failure.Message.ShouldBe($$"""Expected the body to deserialize to 'TourPayload' but it did not: {{inner.Message}} Body: {"id":"seven"}""");
    }

    [Fact]
    public async Task Ok_truncates_a_long_invalid_body_in_the_deserialization_failure()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, "{" + new string('y', 400));
        Func<Task> assertion = async () => await response.ShouldBeOk<TourPayload>(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldEndWith("Body: {" + new string('y', 199) + "... (truncated, 401 bytes in total)", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ok_rejects_a_json_null_body()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, "null");
        Func<Task> assertion = async () => await response.ShouldBeOk<TourPayload>(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected the body to deserialize to 'TourPayload' but it was the JSON value null.");
    }

    [Fact]
    public async Task Ok_with_a_body_type_validates_arguments_and_cancellation()
    {
        // Arrange
        HttpResponseMessage missing = null!;
        using var response = SuccessResponses.Text(HttpStatusCode.OK, """{"id":7,"startCity":"Lisbon"}""");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> nullResponse = async () => await missing.ShouldBeOk<TourPayload>(SuccessResponses.Token);
        Func<Task> nullOptions = async () => await response.ShouldBeOk<TourPayload>((JsonSerializerOptions)null!, SuccessResponses.Token);
        JsonTypeInfo<TourPayload> missingTypeInfo = null!;
        Func<Task> nullTypeInfo = async () => await response.ShouldBeOk(missingTypeInfo, SuccessResponses.Token);
        Func<Task> canceled = async () => await response.ShouldBeOk<TourPayload>(cancellation.Token);

        // Act
        await nullResponse.ShouldThrow<ArgumentNullException>();
        await nullOptions.ShouldThrow<ArgumentNullException>();
        await nullTypeInfo.ShouldThrow<ArgumentNullException>();
        var cancellationFailure = await canceled.ShouldThrow<OperationCanceledException>();

        // Assert
        cancellationFailure.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task Created_reads_the_body_and_requires_the_location_header()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.Created, """{"id":7,"startCity":"Lisbon"}""");
        response.Headers.Location = new Uri("/api/tours/7", UriKind.Relative);

        // Act
        var payload = await response.ShouldBeCreated<TourPayload>(SuccessResponses.Token);
        var optionsPayload = await response.ShouldBeCreated<TourPayload>(JsonSerializerOptions.Web, SuccessResponses.Token);
        var typeInfoPayload = await response.ShouldBeCreated(TourPayloadJsonContext.Default.TourPayload, SuccessResponses.Token);

        // Assert
        payload.ShouldBe(new TourPayload(7, "Lisbon"));
        optionsPayload.ShouldBe(payload);
        typeInfoPayload.ShouldBe(payload);
    }

    [Fact]
    public async Task Created_with_a_body_reports_the_status_before_the_location_and_the_location_before_the_body()
    {
        // Arrange
        using var wrongStatus = SuccessResponses.Text(HttpStatusCode.OK, """{"id":7,"startCity":"Lisbon"}""");
        using var noLocation = SuccessResponses.Text(HttpStatusCode.Created, "not json", "text/plain");
        Func<Task> wrongStatusAssertion = async () => await wrongStatus.ShouldBeCreated<TourPayload>(SuccessResponses.Token);
        Func<Task> noLocationAssertion = async () => await noLocation.ShouldBeCreated<TourPayload>(SuccessResponses.Token);

        // Act
        var statusFailure = await wrongStatusAssertion.ShouldThrow<XunitException>();
        var locationFailure = await noLocationAssertion.ShouldThrow<XunitException>();

        // Assert
        statusFailure.Message.ShouldBe("""Expected HTTP status 201 (Created) but was 200 (OK). Body: {"id":7,"startCity":"Lisbon"}""");
        locationFailure.Message.ShouldBe("Expected a Location header but the response has none.");
    }
}
