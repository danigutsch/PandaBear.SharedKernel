using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace PandaBear.Observability.Tests;

/// <summary>
/// Tests that one call registers the meters and activity sources of every SharedKernel package by their name prefix, so a host exports
/// them without naming each package or referencing it, and leaves other telemetry alone.
/// </summary>
public sealed class SharedKernelInstrumentationTests
{
    [Theory]
    [InlineData("PandaBear.AspNetCore")]
    [InlineData("PandaBear.Blazor")]
    [InlineData("SharedKernel.EventSourcing.Npgsql")]
    [InlineData("SharedKernel.Mediator")]
    [InlineData("SharedKernel.Observability.Npgsql")]
    public void A_shared_kernel_meter_is_collected(string meterName)
    {
        // Arrange
        using var exporter = new DiscardingMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddSharedKernelInstrumentation()
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
    [InlineData("SharedKernel.HttpClients.ContractOutcomes")]
    public void A_shared_kernel_activity_source_is_traced(string sourceName)
    {
        // Arrange
        using var provider = Sdk.CreateTracerProviderBuilder().AddSharedKernelInstrumentation().Build();
        using var source = new ActivitySource(sourceName);

        // Act
        var listened = source.HasListeners();

        // Assert
        listened.ShouldBeTrue(sourceName);
    }

    [Fact]
    public void Other_telemetry_is_left_alone()
    {
        // Arrange
        using var exporter = new DiscardingMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSharedKernelInstrumentation()
            .AddReader(reader)
            .Build();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder().AddSharedKernelInstrumentation().Build();
        using var meter = new Meter("Contoso.Orders");
        using var source = new ActivitySource("Contoso.Orders");

        // Act
        var counter = meter.CreateCounter<long>("probe");

        // Assert
        counter.Enabled.ShouldBeFalse();
        source.HasListeners().ShouldBeFalse();
    }

    [Fact]
    public void The_name_patterns_cover_both_prefixes()
    {
        // Act
        var patterns = SharedKernelInstrumentationExtensions.NamePatterns;

        // Assert
        patterns.ShouldBe(["PandaBear.*", "SharedKernel.*"]);
    }
}
