using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Tests the opt-in authentication and rate-limit security events.</summary>
public sealed class SecurityEventTelemetryTests
{
    private const string SensitiveMessage = "IDX10223: Lifetime validation failed for user alice@example.com";

    [Theory]
    [InlineData(typeof(SecurityTokenExpiredException), "token_expired")]
    [InlineData(typeof(SecurityTokenInvalidAudienceException), "invalid_audience")]
    [InlineData(typeof(SecurityTokenInvalidIssuerException), "invalid_issuer")]
    [InlineData(typeof(SecurityTokenSignatureKeyNotFoundException), "signing_key_not_found")]
    [InlineData(typeof(SecurityTokenInvalidSignatureException), "invalid_signature")]
    [InlineData(typeof(SecurityTokenMalformedException), "malformed_token")]
    [InlineData(typeof(SecurityTokenDecryptionFailedException), "invalid_token")]
    [InlineData(typeof(InvalidOperationException), "authentication_error")]
    public async Task Authentication_failure_is_logged_and_counted_with_a_bounded_reason(Type exceptionType, string reason)
    {
        // Arrange
        using var fixture = SecurityEventTestFixture.Create();
        var options = fixture.JwtOptions();
        var exception = (Exception)Activator.CreateInstance(exceptionType, SensitiveMessage)!;
        var context = new AuthenticationFailedContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options) { Exception = exception };

        // Act
        await options.Events.AuthenticationFailed(context);

        // Assert
        var measurement = fixture.Meters.Measurements.ShouldHaveSingleItem();
        measurement.Instrument.ShouldBe(AspNetCoreTelemetry.AuthenticationEventsCounterName);
        measurement.Tags.Keys.ShouldBeEquivalentTo(AspNetCoreTelemetry.EventTag, AspNetCoreTelemetry.ReasonTag, AspNetCoreTelemetry.SchemeTag);
        measurement.Tags[AspNetCoreTelemetry.EventTag].ShouldBe("authentication_failed");
        measurement.Tags[AspNetCoreTelemetry.ReasonTag].ShouldBe(reason);
        var record = fixture.Logger.Records.ShouldHaveSingleItem();
        record.EventId.Id.ShouldBe(AspNetCoreTelemetry.AuthenticationEventId);
        record.Exception.ShouldBeNull("the exception object must not be attached to the log record");
        fixture.ShouldLeakNothing(SecurityEventTestFixture.Token, SensitiveMessage, "alice@example.com", "secret-subject");
    }

    [Fact]
    public async Task Challenge_without_a_token_is_reported_as_missing_token()
    {
        // Arrange
        using var fixture = SecurityEventTestFixture.Create();
        var options = fixture.JwtOptions();
        var context = new JwtBearerChallengeContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options, new AuthenticationProperties());

        // Act
        await options.Events.Challenge(context);

        // Assert
        var measurement = fixture.Meters.Measurements.ShouldHaveSingleItem();
        measurement.Tags[AspNetCoreTelemetry.EventTag].ShouldBe("challenge");
        measurement.Tags[AspNetCoreTelemetry.ReasonTag].ShouldBe("missing_token");
        fixture.Logger.Records.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task Challenge_after_a_failed_validation_carries_the_bounded_failure_reason_only()
    {
        // Arrange
        using var fixture = SecurityEventTestFixture.Create();
        var options = fixture.JwtOptions();
        var context = new JwtBearerChallengeContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options, new AuthenticationProperties())
        {
            AuthenticateFailure = new SecurityTokenExpiredException(SensitiveMessage),
            Error = SensitiveMessage,
            ErrorDescription = SensitiveMessage
        };

        // Act
        await options.Events.Challenge(context);

        // Assert
        var measurement = fixture.Meters.Measurements.ShouldHaveSingleItem();
        measurement.Tags[AspNetCoreTelemetry.ReasonTag].ShouldBe("token_expired");
        fixture.ShouldLeakNothing(SecurityEventTestFixture.Token, SensitiveMessage, "alice@example.com");
    }

    [Fact]
    public async Task Forbidden_result_is_logged_and_counted_without_claims()
    {
        // Arrange
        using var fixture = SecurityEventTestFixture.Create();
        var options = fixture.JwtOptions();
        var httpContext = fixture.HttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim("sub", "secret-subject"), new System.Security.Claims.Claim("permission", "secret-permission")],
            "test"));
        var context = new ForbiddenContext(httpContext, SecurityEventTestFixture.Scheme(), options);

        // Act
        await options.Events.Forbidden(context);

        // Assert
        var measurement = fixture.Meters.Measurements.ShouldHaveSingleItem();
        measurement.Tags[AspNetCoreTelemetry.EventTag].ShouldBe("forbidden");
        measurement.Tags[AspNetCoreTelemetry.ReasonTag].ShouldBe("insufficient_permission");
        fixture.ShouldLeakNothing(SecurityEventTestFixture.Token, "secret-subject", "secret-permission");
    }

    [Fact]
    public async Task Existing_jwt_bearer_event_handlers_still_run_after_the_report()
    {
        // Arrange
        var ran = false;
        using var fixture = SecurityEventTestFixture.Create(jwt => jwt.Events.OnForbidden = _ =>
        {
            ran = true;
            return Task.CompletedTask;
        });
        var options = fixture.JwtOptions();

        // Act
        await options.Events.Forbidden(new ForbiddenContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options));

        // Assert
        ran.ShouldBeTrue("the host's own handler must keep running");
        fixture.Meters.Measurements.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Rate_limit_rejection_is_logged_and_counted_with_the_policy_name_and_keeps_the_existing_callback()
    {
        // Arrange
        var callbackRan = false;
        using var fixture = SecurityEventTestFixture.Create(rateLimiter: options =>
        {
            options.AddRemoteIpFixedWindowPolicy("login", permitLimit: 1, window: TimeSpan.FromMinutes(1));
            options.OnRejected = (_, _) =>
            {
                callbackRan = true;
                return ValueTask.CompletedTask;
            };
        });
        var httpContext = fixture.HttpContext();
        httpContext.SetEndpoint(new Endpoint(
            requestDelegate: null,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute("login")),
            displayName: "test"));
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var rejected = fixture.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.OnRejected.ShouldNotBeNull();

        // Act
        await rejected(new OnRejectedContext { HttpContext = httpContext, Lease = new RejectedRateLimitLease() }, TestContext.Current.CancellationToken);

        // Assert
        callbackRan.ShouldBeTrue("the host's own callback must keep running");
        var measurement = fixture.Meters.Measurements.ShouldHaveSingleItem();
        measurement.Instrument.ShouldBe(AspNetCoreTelemetry.RateLimitRejectionsCounterName);
        measurement.Tags.Keys.ShouldBeEquivalentTo(AspNetCoreTelemetry.PolicyTag);
        measurement.Tags[AspNetCoreTelemetry.PolicyTag].ShouldBe("login");
        var record = fixture.Logger.Records.ShouldHaveSingleItem();
        record.EventId.Id.ShouldBe(AspNetCoreTelemetry.RateLimitRejectionEventId);
        record.Message.ShouldContain("login", StringComparison.Ordinal);
        fixture.ShouldLeakNothing("203.0.113.9");
    }

    [Fact]
    public async Task Rate_limit_rejection_without_a_named_policy_uses_the_none_policy()
    {
        // Arrange
        using var fixture = SecurityEventTestFixture.Create(rateLimiter: options => options.AddRemoteIpFixedWindowPolicy("login", 1, TimeSpan.FromMinutes(1)));
        var httpContext = fixture.HttpContext();
        var rateLimiterOptions = fixture.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        // Act
        await rateLimiterOptions.OnRejected.ShouldNotBeNull()(
            new OnRejectedContext { HttpContext = httpContext, Lease = new RejectedRateLimitLease() },
            TestContext.Current.CancellationToken);

        // Assert
        fixture.Meters.Measurements.ShouldHaveSingleItem().Tags[AspNetCoreTelemetry.PolicyTag].ShouldBe(AspNetCoreTelemetry.NoPolicy);
    }

    [Fact]
    public async Task Failed_authentication_log_level_is_warning()
    {
        // Arrange
        using var fixture = SecurityEventTestFixture.Create();
        var options = fixture.JwtOptions();

        // Act
        await options.Events.AuthenticationFailed(new AuthenticationFailedContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options)
        {
            Exception = new SecurityTokenExpiredException()
        });

        // Assert
        fixture.Logger.Records.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task Report_happens_before_the_hosts_own_handler_runs()
    {
        // Arrange
        var recordsSeenByHandler = -1;
        SecurityEventTestFixture? holder = null;
        using var fixture = SecurityEventTestFixture.Create(jwt => jwt.Events.OnForbidden = _ =>
        {
            recordsSeenByHandler = holder!.Logger.Records.Count;
            return Task.CompletedTask;
        });
        holder = fixture;
        var options = fixture.JwtOptions();

        // Act
        await options.Events.Forbidden(new ForbiddenContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options));

        // Assert
        recordsSeenByHandler.ShouldBe(1, "the event is reported before the host's handler runs");
    }

    [Fact]
    public async Task Registering_twice_reports_each_event_once()
    {
        // Arrange
        using var fixture = SecurityEventTestFixture.Create(telemetryRegistrations: 2);
        var options = fixture.JwtOptions();

        // Act
        await options.Events.Forbidden(new ForbiddenContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options));

        // Assert
        fixture.Meters.Measurements.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Shared_events_instance_is_wrapped_once_and_reports_the_scheme_that_raised_the_event()
    {
        // Arrange
        using var meters = new MeterCollector();
        var shared = new JwtBearerEvents();
        var services = new ServiceCollection();
        services.AddSingleton<IMeterFactory>(meters);
        services.AddSingleton<ILogger<SecurityEventReporter>>(new RecordingLogger<SecurityEventReporter>());
        services.AddAuthentication()
            .AddJwtBearer("first", options => options.Events = shared)
            .AddJwtBearer("second", options => options.Events = shared);
        services.AddSecurityEventTelemetry();
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var second = monitor.Get("second");
        monitor.Get("first").Events.ShouldBeSameAs(second.Events);
        var httpContext = new DefaultHttpContext { RequestServices = provider };

        // Act
        await shared.Forbidden(new ForbiddenContext(httpContext, new AuthenticationScheme("second", null, typeof(JwtBearerHandler)), second));

        // Assert
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Tags[AspNetCoreTelemetry.SchemeTag].ShouldBe("second");
    }

    [Fact]
    public async Task Scheme_configured_with_an_events_type_is_not_instrumented()
    {
        // Arrange
        using var fixture = SecurityEventTestFixture.Create(jwt => jwt.EventsType = typeof(JwtBearerEvents));
        var options = fixture.JwtOptions();

        // Act
        await options.Events.Forbidden(new ForbiddenContext(fixture.HttpContext(), SecurityEventTestFixture.Scheme(), options));

        // Assert
        fixture.Meters.Measurements.ShouldBeEmpty("EventsType hosts own their events");
    }
}
