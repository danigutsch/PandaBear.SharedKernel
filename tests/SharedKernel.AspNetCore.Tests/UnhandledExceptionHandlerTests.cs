using System.Net;
using System.Text.Json;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Tests the opt-in unhandled-exception handler end to end through <c>UseExceptionHandler</c>.</summary>
public sealed class UnhandledExceptionHandlerTests
{
    private const string SensitiveMessage = "secret-connection-string password=hunter2";

    [Fact]
    public async Task Unhandled_exception_returns_problem_details_with_trace_id_and_no_exception_text()
    {
        // Arrange
        using var meters = new MeterCollector();
        var logger = new RecordingLogger<UnhandledExceptionReporter>();
        await using var app = await UnhandledExceptionTestApp.Start(meters, logger);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };

        // Act
        using var response = await client.GetAsync(new Uri("/orders/42", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        mediaType.ShouldBe("application/problem+json");
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
        var traceId = document.RootElement.GetProperty("traceId").GetString();
        traceId.ShouldNotBeNull().ShouldNotBe(string.Empty);
        body.ShouldNotContain(SensitiveMessage, "the exception message must never reach the response");
        body.ShouldNotContain("InvalidOperationException", "the exception type must never reach the response");
    }

    [Fact]
    public async Task Unhandled_exception_logs_one_stable_event_with_only_bounded_values()
    {
        // Arrange
        using var meters = new MeterCollector();
        var logger = new RecordingLogger<UnhandledExceptionReporter>();
        await using var app = await UnhandledExceptionTestApp.Start(meters, logger);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };

        // Act
        using var response = await client.GetAsync(new Uri("/orders/42", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        var record = logger.Records.ShouldHaveSingleItem();
        record.EventId.Id.ShouldBe(AspNetCoreTelemetry.UnhandledExceptionEventId);
        record.Exception.ShouldBeNull("the exception object must not be attached to the log record");
        record.Message.ShouldNotContain(SensitiveMessage, "the exception message must never reach the log");
        record.Message.ShouldNotContain("/orders/42", "the raw path must not reach the log");
        var keys = record.State.Select(static pair => pair.Key).Where(static key => key != "{OriginalFormat}");
        keys.ShouldBeEquivalentTo("ErrorType", "RouteTemplate", "StatusCode", "TraceId");
        record.State.ShouldContain(new KeyValuePair<string, object?>("ErrorType", nameof(InvalidOperationException)));
        record.State.ShouldContain(new KeyValuePair<string, object?>("RouteTemplate", "/orders/{id}"));
        record.State.ShouldContain(new KeyValuePair<string, object?>("StatusCode", 500));
    }

    [Fact]
    public async Task Unhandled_exception_increments_a_counter_with_only_bounded_tags()
    {
        // Arrange
        using var meters = new MeterCollector();
        var logger = new RecordingLogger<UnhandledExceptionReporter>();
        await using var app = await UnhandledExceptionTestApp.Start(meters, logger);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };

        // Act
        using var response = await client.GetAsync(new Uri("/orders/42", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Instrument.ShouldBe(AspNetCoreTelemetry.UnhandledExceptionsCounterName);
        measurement.Value.ShouldBe(1L);
        measurement.Tags.Keys.ShouldBeEquivalentTo(
            AspNetCoreTelemetry.ErrorTypeTag,
            AspNetCoreTelemetry.RouteTemplateTag,
            AspNetCoreTelemetry.HandledTag);
        measurement.Tags[AspNetCoreTelemetry.ErrorTypeTag].ShouldBe(nameof(InvalidOperationException));
        measurement.Tags[AspNetCoreTelemetry.RouteTemplateTag].ShouldBe("/orders/{id}");
        measurement.Tags[AspNetCoreTelemetry.HandledTag].ShouldBe(true);
    }

    [Fact]
    public void Reporter_called_outside_a_request_counts_the_exception_as_handled_with_an_unmatched_route()
    {
        // Arrange
        using var meters = new MeterCollector();
        var logger = new RecordingLogger<UnhandledExceptionReporter>();
        var reporter = new UnhandledExceptionReporter(logger, meters);

        // Act
        reporter.Report(new InvalidOperationException(SensitiveMessage), routeTemplate: null, statusCode: 0, traceId: "trace-1", handled: true);

        // Assert
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Tags[AspNetCoreTelemetry.RouteTemplateTag].ShouldBe(AspNetCoreTelemetry.UnmatchedRoute);
        measurement.Tags[AspNetCoreTelemetry.HandledTag].ShouldBe(true);
        logger.Records.ShouldHaveSingleItem().Message.ShouldNotContain(SensitiveMessage, "the exception message must never reach the log");
    }

    [Fact]
    public async Task Bad_http_request_keeps_its_status_and_is_not_counted_as_an_unhandled_exception()
    {
        // Arrange
        using var meters = new MeterCollector();
        var logger = new RecordingLogger<UnhandledExceptionReporter>();
        await using var app = await UnhandledExceptionTestApp.Start(meters, logger);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };

        // Act
        using var response = await client.PostAsync(new Uri("/upload", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        body.ShouldNotContain(SensitiveMessage, "the exception message must never reach the response");
        meters.Measurements.ShouldBeEmpty("a client error is not an unhandled exception");
        logger.Records.ShouldBeEmpty("a client error is not logged as an unhandled exception");
    }

    [Fact]
    public async Task Client_abort_is_left_to_the_framework_and_not_counted()
    {
        // Arrange
        using var meters = new MeterCollector();
        var logger = new RecordingLogger<UnhandledExceptionReporter>();
        var handler = new UnhandledExceptionHandler(new UnhandledExceptionReporter(logger, meters));
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestAborted = aborted.Token };

        // Act
        var handled = await handler.TryHandleAsync(httpContext, new OperationCanceledException(), TestContext.Current.CancellationToken);

        // Assert
        handled.ShouldBeFalse("the framework answers an aborted request");
        meters.Measurements.ShouldBeEmpty("an aborted request is not an unhandled exception");
    }

    [Fact]
    public void Exception_after_the_response_started_can_be_reported_explicitly_as_not_handled()
    {
        // Arrange
        using var meters = new MeterCollector();
        var reporter = new UnhandledExceptionReporter(new RecordingLogger<UnhandledExceptionReporter>(), meters);
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();

        // Act
        reporter.Report(httpContext, new InvalidOperationException(SensitiveMessage), handled: false);

        // Assert
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Tags[AspNetCoreTelemetry.HandledTag].ShouldBe(false);
        measurement.Tags[AspNetCoreTelemetry.RouteTemplateTag].ShouldBe(AspNetCoreTelemetry.UnmatchedRoute);
    }

    [Theory]
    [InlineData("/orders/{id}?token=abc", "/orders/{id}")]
    [InlineData("/orders/{id:int}#frag", "/orders/{id:int}")]
    [InlineData("https://example.test/orders/42", AspNetCoreTelemetry.UnmatchedRoute)]
    [InlineData("mailto:someone@example.test", AspNetCoreTelemetry.UnmatchedRoute)]
    [InlineData("  ", AspNetCoreTelemetry.UnmatchedRoute)]
    [InlineData("?token=abc", AspNetCoreTelemetry.UnmatchedRoute)]
    public void Route_template_is_normalized_defensively(string input, string expected)
    {
        // Arrange
        using var meters = new MeterCollector();
        var reporter = new UnhandledExceptionReporter(new RecordingLogger<UnhandledExceptionReporter>(), meters);

        // Act
        reporter.Report(new InvalidOperationException(), input, 500, "trace", handled: true);

        // Assert
        meters.Measurements.ShouldHaveSingleItem().Tags[AspNetCoreTelemetry.RouteTemplateTag].ShouldBe(expected);
    }

    [Fact]
    public void Aggregate_and_reflection_wrappers_report_the_inner_exception_type()
    {
        // Arrange
        using var meters = new MeterCollector();
        var reporter = new UnhandledExceptionReporter(new RecordingLogger<UnhandledExceptionReporter>(), meters);

        // Act
        reporter.Report(new AggregateException(new InvalidOperationException()), "/a", 500, "t", handled: true);
        reporter.Report(new System.Reflection.TargetInvocationException(new ArgumentException()), "/a", 500, "t", handled: true);
        reporter.Report(new TimeoutException(), "/a", 500, "t", handled: true);

        // Assert
        meters.Measurements.Select(static m => m.Tags[AspNetCoreTelemetry.ErrorTypeTag])
            .ShouldBeEquivalentTo("InvalidOperationException", "ArgumentException", "TimeoutException");
    }
}
