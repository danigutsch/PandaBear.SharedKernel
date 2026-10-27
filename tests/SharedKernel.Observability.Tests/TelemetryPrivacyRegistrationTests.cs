using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace PandaBear.Observability.Tests;

/// <summary>Tests that <see cref="ObservabilityBuilderExtensions.AddTelemetryPrivacy{TBuilder}"/> wires both privacy processors ahead of host-added exporters.</summary>
[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class TelemetryPrivacyRegistrationTests
{
    [Fact]
    public void Add_telemetry_privacy_returns_builder()
    {
        // Arrange
        var builder = new HostApplicationBuilder();

        // Act
        var result = builder.AddTelemetryPrivacy();

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Add_telemetry_privacy_strips_log_messages_scopes_exceptions_and_host_private_attributes()
    {
        // Arrange
        var records = new ConcurrentQueue<CapturedPrivacyLogRecord>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.ConfigureOpenTelemetry().AddTelemetryPrivacy(static name => name == "HostPrivate");
        builder.Logging.AddOpenTelemetry(logging =>
            logging.AddProcessor(_ => new SimpleLogRecordExportProcessor(new PrivacyLogExporter(records))));
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<TelemetryPrivacyRegistrationTests>>();
        using var logScope = logger.BeginScope(new Dictionary<string, object?> { ["scoped"] = "private@example.test" });
        var log = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1),
            "Operation for {HostPrivate} ended with {Outcome}");

        // Act
        log(logger, "private@example.test", "failed", new InvalidOperationException("private@example.test"));

        // Assert
        var result = records.ShouldHaveSingleItem();
        result.FormattedMessage.ShouldBeNull();
        result.Exception.ShouldBeNull();
        result.ScopeCount.ShouldBe(0);
        result.Attributes.ShouldNotContain(attribute => attribute.Key == "HostPrivate");
        result.Attributes.ShouldContain(attribute => attribute.Key == "Outcome" && Equals(attribute.Value, "failed"));
    }

    [Fact]
    public void Add_telemetry_privacy_strips_sensitive_trace_tags_and_host_private_tags()
    {
        // Arrange
        var exported = new ConcurrentQueue<Activity>();
        using var source = new ActivitySource($"privacy-registration-{Guid.NewGuid():N}");
        var builder = Host.CreateApplicationBuilder();
        builder.AddTelemetryPrivacy(static name => name == "host.private");
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
            .AddSource(source.Name)
            .AddProcessor(new SimpleActivityExportProcessor(new PrivacyActivityExporter(exported))));
        using var host = builder.Build();
        host.Start();

        // Act
        using (var activity = source.StartActivity("operation").ShouldNotBeNull())
        {
            activity.SetTag("email", "private@example.test");
            activity.SetTag("host.private", "private");
            activity.SetTag("http.route", "/records/{id}");
        }

        // Assert
        var result = exported.ShouldHaveSingleItem();
        result.TagObjects.ShouldNotContain(tag => tag.Key is "email" or "host.private");
        result.TagObjects.ShouldContain(tag => tag.Key == "http.route");
    }
}
