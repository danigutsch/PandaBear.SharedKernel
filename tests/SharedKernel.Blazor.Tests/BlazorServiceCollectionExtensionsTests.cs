using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the registration of the UI failure reporter.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class BlazorServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUiFailureReporting_registers_the_reporter_as_a_singleton()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging().AddMetrics();

        // Act
        var returned = services.AddUiFailureReporting();
        using var provider = services.BuildServiceProvider();

        // Assert
        returned.ShouldBeSameAs(services);
        var reporter = provider.GetRequiredService<IUiFailureReporter>();
        reporter.ShouldBeOfType<UiFailureReporter>();
        provider.GetRequiredService<IUiFailureReporter>().ShouldBeSameAs(reporter);
    }

    [Fact]
    public void AddUiFailureReporting_keeps_an_existing_reporter()
    {
        // Arrange
        var existing = new RecordingFailureReporter();
        var services = new ServiceCollection().AddSingleton<IUiFailureReporter>(existing);

        // Act
        services.AddUiFailureReporting();
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IUiFailureReporter>().ShouldBeSameAs(existing);
    }

    [Fact]
    public void AddUiFailureReporting_resolves_with_the_metrics_services()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddMetrics()
            .AddUiFailureReporting();
        using var provider = services.BuildServiceProvider();

        // Act
        var failure = Record.Exception(() => provider.GetRequiredService<IUiFailureReporter>().Report(new InvalidOperationException(), "op"));

        // Assert
        failure.ShouldBeNull();
    }

    [Fact]
    public void AddUiFailureReporting_resolves_without_the_caller_registering_metrics()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddUiFailureReporting();
        using var provider = services.BuildServiceProvider();

        // Act
        var reporter = provider.GetRequiredService<IUiFailureReporter>();
        var failure = Record.Exception(() => reporter.Report(new InvalidOperationException(), "op"));

        // Assert
        reporter.ShouldBeOfType<UiFailureReporter>();
        failure.ShouldBeNull();
        provider.GetService<IMeterFactory>().ShouldNotBeNull();
    }

    [Fact]
    public void AddUiFailureReporting_keeps_a_meter_factory_and_counts_when_metrics_are_added_around_it()
    {
        // Arrange
        using var meters = new MeterCollector();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IMeterFactory>(meters)
            .AddMetrics()
            .AddUiFailureReporting()
            .AddMetrics();
        using var provider = services.BuildServiceProvider();

        // Act
        provider.GetRequiredService<IUiFailureReporter>().Report(new InvalidOperationException(), "op");

        // Assert
        provider.GetRequiredService<IMeterFactory>().ShouldBeSameAs(meters);
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Instrument.ShouldBe(UiFailureReporter.HandledFailuresCounterName);
        measurement.Value.ShouldBe(1);
    }

    [Fact]
    public void AddUiFailureReporting_rejects_a_null_collection()
    {
        // Act
        Action act = () => BlazorServiceCollectionExtensions.AddUiFailureReporting(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
