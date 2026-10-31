using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace PandaBear.Observability.Tests;

/// <summary>Tests that <see cref="ObservabilityBuilderExtensions.ConfigureOpenTelemetry{TBuilder}"/> applies the privacy baseline without a separate opt-in call.</summary>
[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class ConfigureOpenTelemetryPrivacyDefaultTests
{
    [Fact]
    public void Configure_opentelemetry_alone_strips_log_messages_scopes_exceptions_and_sensitive_attributes()
    {
        // Arrange
        var records = new ConcurrentQueue<CapturedPrivacyLogRecord>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.ConfigureOpenTelemetry();
        builder.Logging.AddOpenTelemetry(logging =>
            logging.AddProcessor(_ => new SimpleLogRecordExportProcessor(new PrivacyLogExporter(records))));
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<ConfigureOpenTelemetryPrivacyDefaultTests>>();
        using var logScope = logger.BeginScope(new Dictionary<string, object?> { ["scoped"] = "private@example.test" });
        var log = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1),
            "Operation for {Email} ended with {Outcome}");

        // Act
        log(logger, "private@example.test", "failed", new InvalidOperationException("private@example.test"));

        // Assert
        var result = records.ShouldHaveSingleItem();
        result.FormattedMessage.ShouldBeNull();
        result.Exception.ShouldBeNull();
        result.ScopeCount.ShouldBe(0);
        result.Attributes.ShouldNotContain(attribute => attribute.Key == "Email");
        result.Attributes.ShouldContain(attribute => attribute.Key == "Outcome" && Equals(attribute.Value, "failed"));
    }

    [Fact]
    public void Configure_opentelemetry_alone_strips_sensitive_trace_tags()
    {
        // Arrange
        var exported = new ConcurrentQueue<Activity>();
        using var source = new ActivitySource($"privacy-default-{Guid.NewGuid():N}");
        var builder = Host.CreateApplicationBuilder();
        builder.ConfigureOpenTelemetry();
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
            .AddSource(source.Name)
            .AddProcessor(new SimpleActivityExportProcessor(new PrivacyActivityExporter(exported))));
        using var host = builder.Build();
        host.Start();

        // Act
        using (var activity = source.StartActivity("operation").ShouldNotBeNull())
        {
            activity.SetTag("email", "private@example.test");
            activity.SetTag("http.route", "/records/{id}");
        }

        // Assert
        var result = exported.ShouldHaveSingleItem();
        result.TagObjects.ShouldNotContain(tag => tag.Key == "email");
        result.TagObjects.ShouldContain(tag => tag.Key == "http.route");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Host_private_attributes_are_removed_whichever_order_the_two_calls_run(bool configureFirst)
    {
        // Arrange
        var records = new ConcurrentQueue<CapturedPrivacyLogRecord>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        if (configureFirst)
        {
            builder.ConfigureOpenTelemetry().AddTelemetryPrivacy(static name => name == "HostPrivate");
        }
        else
        {
            builder.AddTelemetryPrivacy(static name => name == "HostPrivate").ConfigureOpenTelemetry();
        }

        builder.Logging.AddOpenTelemetry(logging =>
            logging.AddProcessor(_ => new SimpleLogRecordExportProcessor(new PrivacyLogExporter(records))));
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<ConfigureOpenTelemetryPrivacyDefaultTests>>();
        var log = LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2),
            "Operation for {HostPrivate} ended with {Outcome}");

        // Act
        log(logger, "private", "ok", null);

        // Assert
        var result = records.ShouldHaveSingleItem();
        result.Attributes.ShouldNotContain(attribute => attribute.Key == "HostPrivate");
        result.Attributes.ShouldContain(attribute => attribute.Key == "Outcome" && Equals(attribute.Value, "ok"));
    }
}
