using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PandaBear.Observability;
using PandaBear.Observability.Tests;

namespace ObservabilityConsumer;

/// <summary>
/// Tests that the non-generic <c>AddInstrumentation()</c> resolves beside the generic <c>AddInstrumentation&lt;T&gt;</c> overloads that
/// OpenTelemetry declares on the same builders. The namespace lies outside <c>PandaBear</c>, as a consuming host's does, so both sets of
/// extensions come from using directives in the same scope and an ambiguous call would not compile.
/// </summary>
public sealed class InstrumentationOverloadTests
{
    [Fact]
    public void The_generic_meter_provider_overloads_resolve_beside_the_non_generic_call()
    {
        // Arrange
        using var registered = new ProbeInstrumentation();
        using var instance = new ProbeInstrumentation();
        using var fromFactory = new ProbeInstrumentation();
        using var fromServices = new ProbeInstrumentation();
        using var fromServicesAndProvider = new ProbeInstrumentation();
        ProbeInstrumentation[] probes = [registered, instance, fromFactory, fromServices, fromServicesAndProvider];
        using var exporter = new DiscardingMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        using var meter = new Meter("SharedKernel.Probe");
        var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services => services.AddSingleton(registered))
            .AddInstrumentation()
            .AddInstrumentation<ProbeInstrumentation>()
            .AddInstrumentation(instance)
            .AddInstrumentation(() => fromFactory)
            .AddInstrumentation(_ => fromServices)
            .AddInstrumentation((_, _) => fromServicesAndProvider)
            .AddReader(reader)
            .Build();

        // Act
        var enabled = meter.CreateCounter<long>("probe").Enabled;
        provider.Dispose();

        // Assert
        enabled.ShouldBeTrue();
        probes.Where(static probe => !probe.Disposed).ShouldBeEmpty();
    }

    [Fact]
    public void The_generic_tracer_provider_overloads_resolve_beside_the_non_generic_call()
    {
        // Arrange
        using var registered = new ProbeInstrumentation();
        using var instance = new ProbeInstrumentation();
        using var fromFactory = new ProbeInstrumentation();
        using var fromServices = new ProbeInstrumentation();
        using var fromServicesAndProvider = new ProbeInstrumentation();
        ProbeInstrumentation[] probes = [registered, instance, fromFactory, fromServices, fromServicesAndProvider];
        using var source = new ActivitySource("SharedKernel.Probe");
        var provider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton(registered))
            .AddInstrumentation()
            .AddInstrumentation<ProbeInstrumentation>()
            .AddInstrumentation(instance)
            .AddInstrumentation(() => fromFactory)
            .AddInstrumentation(_ => fromServices)
            .AddInstrumentation((_, _) => fromServicesAndProvider)
            .Build();

        // Act
        var listened = source.HasListeners();
        provider.Dispose();

        // Assert
        listened.ShouldBeTrue();
        probes.Where(static probe => !probe.Disposed).ShouldBeEmpty();
    }
}
