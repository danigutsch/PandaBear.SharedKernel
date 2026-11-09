using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PandaBear.AspNetCore.RateLimiting.Tests;

/// <summary>Builds a service provider with the rate-limit event telemetry, a recording logger and a metric collector.</summary>
internal sealed class SecurityEventTestFixture : IDisposable
{
    private readonly ServiceProvider _provider;

    private SecurityEventTestFixture(ServiceProvider provider, MeterCollector meters, RecordingLogger<SecurityEventReporter> logger)
    {
        _provider = provider;
        Meters = meters;
        Logger = logger;
    }

    /// <summary>The collector for the package meters.</summary>
    public MeterCollector Meters
    {
        get;
    }

    /// <summary>The recording logger.</summary>
    public RecordingLogger<SecurityEventReporter> Logger
    {
        get;
    }

    /// <summary>The service provider.</summary>
    public IServiceProvider Services => _provider;

    /// <summary>Creates the fixture; the optional callback configures the rate limiter options before telemetry is registered.</summary>
    public static SecurityEventTestFixture Create(Action<RateLimiterOptions>? rateLimiter = null,
        ILogger<SecurityEventReporter>? logger = null, IMeterFactory? meterFactory = null)
    {
        var meters = new MeterCollector(AspNetCoreTelemetry.MeterName);
        var recording = new RecordingLogger<SecurityEventReporter>();
        var services = new ServiceCollection();
        services.AddSingleton(meterFactory ?? meters);
        services.AddSingleton(logger ?? recording);
        services.AddRateLimiter(options => rateLimiter?.Invoke(options));
        services.AddRateLimitEventTelemetry();

        return new SecurityEventTestFixture(services.BuildServiceProvider(), meters, recording);
    }

    /// <summary>Creates a request with a client address.</summary>
    public DefaultHttpContext HttpContext()
    {
        var context = new DefaultHttpContext { RequestServices = _provider };
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");
        return context;
    }

    /// <summary>Asserts that no log record or metric tag contains any of the secrets.</summary>
    public void ShouldLeakNothing(params string[] secrets)
    {
        foreach (var secret in secrets)
        {
            foreach (var record in Logger.Records)
            {
                record.Message.ShouldNotContain(secret, "log messages must not carry secrets");
                record.State.Select(static pair => Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
                    .ShouldNotContain(value => value.Contains(secret, StringComparison.Ordinal), "log state must not carry secrets");
            }

            foreach (var measurement in Meters.Measurements)
            {
                measurement.Tags.Values
                    .Select(static value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
                    .ShouldNotContain(value => value.Contains(secret, StringComparison.Ordinal), "metric tags must not carry secrets");
            }
        }
    }

    /// <summary>Disposes the provider and collector.</summary>
    public void Dispose()
    {
        _provider.Dispose();
        Meters.Dispose();
    }
}
