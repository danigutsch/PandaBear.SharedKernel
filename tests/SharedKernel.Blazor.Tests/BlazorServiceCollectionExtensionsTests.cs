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
    public void AddUiFailureReporting_rejects_a_null_collection()
    {
        // Act
        Action act = () => BlazorServiceCollectionExtensions.AddUiFailureReporting(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
