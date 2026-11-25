using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace SharedKernel.ServiceDefaults.Tests;

/// <summary>Tests the OpenTelemetry part of the service defaults: the probe filter, the privacy processors and the OTLP switch.</summary>
public sealed class TelemetryDefaultsTests
{
    [Fact]
    public async Task Health_and_alive_requests_are_not_traced_while_other_requests_are()
    {
        // Arrange
        using var exporter = new RecordingActivityExporter();
        await using var app = await DefaultsApplication.Start("Production", arrange: builder => builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(exporter))));
        using var client = app.GetTestClient();

        // Act
        _ = await client.GetStringAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        _ = await client.GetStringAsync(new Uri("/ALIVE", UriKind.Relative), TestContext.Current.CancellationToken);
        _ = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);
        _ = await exporter.WaitForRoute("/ping");

        // Assert
        exporter.Exported.Select(activity => activity.GetTagItem("http.route") as string).ShouldBe(["/ping"]);
    }

    [Fact]
    public async Task Exported_spans_lose_the_baseline_and_the_host_named_sensitive_attributes()
    {
        // Arrange
        using var exporter = new RecordingActivityExporter();
        await using var app = await DefaultsApplication.Start(
            "Production",
            options => options.SensitiveTelemetryAttributes.Add("APP.SECRET"),
            builder => builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(exporter))));
        using var client = app.GetTestClient();

        // Act
        _ = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);
        var span = await exporter.WaitForRoute("/ping");

        // Assert
        var tags = span.TagObjects.Select(tag => tag.Key).ToArray();
        tags.ShouldNotContain("email");
        tags.ShouldNotContain("app.secret");
        tags.ShouldContain("app.kept");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("http://127.0.0.1:4318", true)]
    public void The_otlp_exporter_is_added_only_when_an_endpoint_is_configured(string endpoint, bool expected)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration[DefaultsApplication.OtlpEndpointKey] = endpoint;
        var otlpAssembly = typeof(OtlpExporterOptions).Assembly;

        // Act
        builder.AddApplicationDefaults();

        // Assert
        builder.Services.SelectMany(RegisteredTypes.Of).Any(type => type.Assembly == otlpAssembly).ShouldBe(expected);
    }
}
