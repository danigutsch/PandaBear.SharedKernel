using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Tests the security events through the real JwtBearer handler and the rate-limiter middleware.</summary>
public sealed class SecurityEventPipelineTests
{
    private const string BadToken = "not.a.jwt-secret-value";

    [Fact]
    public async Task Request_without_a_token_is_challenged_and_reported_as_missing_token()
    {
        // Arrange
        using var meters = new MeterCollector(AspNetCoreTelemetry.MeterName);
        var logger = new RecordingLogger<SecurityEventReporter>();
        await using var app = await SecurityEventTestApp.Start(meters, logger);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };

        // Act
        using var response = await client.GetAsync(new Uri("/secure", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Tags[AspNetCoreTelemetry.EventTag].ShouldBe("challenge");
        measurement.Tags[AspNetCoreTelemetry.ReasonTag].ShouldBe("missing_token");
        measurement.Tags[AspNetCoreTelemetry.SchemeTag].ShouldBe("Bearer");
    }

    [Fact]
    public async Task Request_with_a_bad_token_reports_a_bounded_failure_and_never_the_token()
    {
        // Arrange
        using var meters = new MeterCollector(AspNetCoreTelemetry.MeterName);
        var logger = new RecordingLogger<SecurityEventReporter>();
        await using var app = await SecurityEventTestApp.Start(meters, logger);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/secure", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BadToken);

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var failed = meters.Measurements.Where(static m => (string?)m.Tags[AspNetCoreTelemetry.EventTag] == "authentication_failed").ToArray().ShouldHaveSingleItem();
        failed.Tags[AspNetCoreTelemetry.ReasonTag].ShouldBeOneOf("malformed_token", "invalid_token", "authentication_error");
        foreach (var record in logger.Records)
        {
            record.Message.ShouldNotContain(BadToken, "the token must never be logged");
            record.Exception.ShouldBeNull("the exception object must not be attached");
        }
    }

    [Fact]
    public async Task Authenticated_request_failing_a_policy_is_reported_as_forbidden_through_the_real_handler()
    {
        // Arrange
        using var meters = new MeterCollector(AspNetCoreTelemetry.MeterName);
        var logger = new RecordingLogger<SecurityEventReporter>();
        await using var app = await SecurityEventTestApp.Start(meters, logger);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };
        var token = SecurityEventTestApp.CreateValidToken();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/admin", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Tags[AspNetCoreTelemetry.EventTag].ShouldBe("forbidden");
        measurement.Tags[AspNetCoreTelemetry.ReasonTag].ShouldBe("insufficient_permission");
        measurement.Tags[AspNetCoreTelemetry.SchemeTag].ShouldBe("Bearer");
        foreach (var record in logger.Records)
        {
            record.Message.ShouldNotContain(token, "the token must never be logged");
        }
    }

    [Fact]
    public async Task Rate_limited_request_is_reported_with_the_policy_name_through_the_real_middleware()
    {
        // Arrange
        using var meters = new MeterCollector(AspNetCoreTelemetry.MeterName);
        var logger = new RecordingLogger<SecurityEventReporter>();
        await using var app = await SecurityEventTestApp.Start(meters, logger);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };
        using var first = await client.GetAsync(new Uri("/login", UriKind.Relative), TestContext.Current.CancellationToken);

        // Act
        using var second = await client.GetAsync(new Uri("/login", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe((HttpStatusCode)StatusCodes.Status429TooManyRequests);
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Instrument.ShouldBe(AspNetCoreTelemetry.RateLimitRejectionsCounterName);
        measurement.Tags[AspNetCoreTelemetry.PolicyTag].ShouldBe("login");
    }
}
