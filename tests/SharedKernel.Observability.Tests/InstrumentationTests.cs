using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace SharedKernel.Observability.Tests;

/// <summary>
/// Tests that one call registers the meters and activity sources of every SharedKernel package by their name prefix, so a host exports
/// them without naming each package or referencing it, and leaves other telemetry alone.
/// </summary>
public sealed class InstrumentationTests
{
    [Theory]
    [InlineData("SharedKernel.AspNetCore")]
    [InlineData("SharedKernel.AspNetCore.WebAnalytics")]
    [InlineData("SharedKernel.Blazor")]
    [InlineData("SharedKernel.MalwareScanning.ClamAv")]
    [InlineData("SharedKernel.Scheduling")]
    [InlineData("SharedKernel.EventSourcing.Npgsql")]
    [InlineData("SharedKernel.Mediator")]
    [InlineData("SharedKernel.Observability.Npgsql")]
    public void A_shared_kernel_meter_is_collected(string meterName)
    {
        // Arrange
        using var exporter = new DiscardingMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddInstrumentation()
            .AddReader(reader)
            .Build();
        using var meter = new Meter(meterName);

        // Act
        var counter = meter.CreateCounter<long>("probe");

        // Assert
        counter.Enabled.ShouldBeTrue(meterName);
    }

    [Theory]
    [InlineData("SharedKernel.EventSourcing.Npgsql")]
    [InlineData("SharedKernel.Mediator")]
    [InlineData("SharedKernel.HttpClients")]
    [InlineData("SharedKernel.MalwareScanning.ClamAv")]
    [InlineData("SharedKernel.ObjectStorage.S3")]
    [InlineData("SharedKernel.Scheduling")]
    public void A_shared_kernel_activity_source_is_traced(string sourceName)
    {
        // Arrange
        using var provider = Sdk.CreateTracerProviderBuilder().AddInstrumentation().Build();
        using var source = new ActivitySource(sourceName);

        // Act
        var listened = source.HasListeners();

        // Assert
        listened.ShouldBeTrue(sourceName);
    }

    [Theory]
    [InlineData("Contoso.Orders")]
    [InlineData("PandaBear.SharedKernel.AspNetCore")]
    public void Other_telemetry_is_left_alone(string name)
    {
        // Arrange
        using var exporter = new DiscardingMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInstrumentation()
            .AddReader(reader)
            .Build();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder().AddInstrumentation().Build();
        using var meter = new Meter(name);
        using var source = new ActivitySource(name);

        // Act
        var counter = meter.CreateCounter<long>("probe");

        // Assert
        counter.Enabled.ShouldBeFalse();
        source.HasListeners().ShouldBeFalse();
    }

    [Fact]
    public void The_name_patterns_cover_only_the_shared_kernel_prefix()
    {
        // Act
        var patterns = InstrumentationExtensions.NamePatterns;

        // Assert
        patterns.ShouldBe(["SharedKernel.*"]);
    }
}
