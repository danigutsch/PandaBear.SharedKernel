using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests privacy-safe reporting of handled UI failures.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class UiFailureReporterTests
{
    [Fact]
    public void Report_logs_only_type_operation_and_trace_id_and_increments_the_counter()
    {
        // Arrange
        var logger = new CapturingLogger<UiFailureReporter>();
        using var meters = new MeterCollector();
        var reporter = new UiFailureReporter(logger, meters);
        using var activity = new Activity("test");
        activity.Start();
        var exception = new InvalidOperationException(HostileText.StackTrace);

        // Act
        reporter.Report(exception, "customers.import");

        // Assert
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(UiFailureReporter.HandledFailureEventId);
        entry.EventId.Name.ShouldBe("HandledUiFailure");
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Exception.ShouldBeNull();
        entry.Message.ShouldContain(typeof(InvalidOperationException).FullName!, StringComparison.Ordinal);
        entry.Message.ShouldContain("customers.import", StringComparison.Ordinal);
        entry.Message.ShouldContain(activity.TraceId.ToString(), StringComparison.Ordinal);
        entry.Message.ShouldNotContain("secret", StringComparison.Ordinal);
        entry.Message.ShouldNotContain("Orders.cs", StringComparison.Ordinal);
        entry.Values.Values.Any(value => value is not null && value.Contains("secret", StringComparison.Ordinal)).ShouldBeFalse();
        var measurement = meters.Measurements.ShouldHaveSingleItem();
        measurement.Instrument.ShouldBe(UiFailureReporter.HandledFailuresCounterName);
        measurement.Value.ShouldBe(1);
        measurement.Tags.Count.ShouldBe(2);
        measurement.Tags[UiFailureReporter.ErrorTypeTag].ShouldBe(nameof(InvalidOperationException));
        measurement.Tags[UiFailureReporter.OperationTag].ShouldBe("customers.import");
    }

    [Fact]
    public void Report_works_without_an_ambient_activity()
    {
        // Arrange
        var logger = new CapturingLogger<UiFailureReporter>();
        using var meters = new MeterCollector();
        var reporter = new UiFailureReporter(logger, meters);

        // Act
        reporter.Report(new TimeoutException("never logged"), "customers.import");

        // Assert
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Message.ShouldNotContain("never logged", StringComparison.Ordinal);
        entry.Values["TraceId"].ShouldBe(UiFailureReporter.UnknownTraceId);
    }

    [Theory]
    [InlineData(HostileText.Markup)]
    [InlineData(HostileText.MultilineStackTrace)]
    [InlineData("")]
    [InlineData("  ")]
    public void Unsafe_operation_names_are_replaced_by_a_constant(string operation)
    {
        // Arrange
        var logger = new CapturingLogger<UiFailureReporter>();
        using var meters = new MeterCollector();
        var reporter = new UiFailureReporter(logger, meters);

        // Act
        reporter.Report(new InvalidOperationException(), operation);

        // Assert
        logger.Entries.ShouldHaveSingleItem().Values["Operation"].ShouldBe(UiFailureReporter.UnknownOperation);
    }

    [Fact]
    public void Overlong_operation_names_are_replaced_by_a_constant()
    {
        // Arrange
        var logger = new CapturingLogger<UiFailureReporter>();
        using var meters = new MeterCollector();
        var reporter = new UiFailureReporter(logger, meters);

        // Act
        reporter.Report(new InvalidOperationException(), HostileText.VeryLong(10_000));

        // Assert
        logger.Entries.ShouldHaveSingleItem().Values["Operation"].ShouldBe(UiFailureReporter.UnknownOperation);
    }

    [Fact]
    public void Null_exception_is_rejected()
    {
        // Arrange
        using var meters = new MeterCollector();
        var reporter = new UiFailureReporter(new CapturingLogger<UiFailureReporter>(), meters);

        // Act
        var report = () => reporter.Report(null!, "operation");

        // Assert
        report.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Unsafe_operation_is_counted_as_unknown()
    {
        // Arrange
        using var meters = new MeterCollector();
        var reporter = new UiFailureReporter(new CapturingLogger<UiFailureReporter>(), meters);

        // Act
        reporter.Report(new InvalidOperationException(), HostileText.Markup);

        // Assert
        meters.Measurements.ShouldHaveSingleItem().Tags[UiFailureReporter.OperationTag].ShouldBe(UiFailureReporter.UnknownOperation);
    }

    [Fact]
    public void Aggregate_exception_is_counted_by_its_first_inner_type()
    {
        // Arrange
        using var meters = new MeterCollector();
        var reporter = new UiFailureReporter(new CapturingLogger<UiFailureReporter>(), meters);

        // Act
        reporter.Report(new AggregateException(new TimeoutException(HostileText.StackTrace)), "op");

        // Assert
        meters.Measurements.ShouldHaveSingleItem().Tags[UiFailureReporter.ErrorTypeTag].ShouldBe(nameof(TimeoutException));
    }

    [Fact]
    public void Target_invocation_exception_is_counted_by_its_inner_type()
    {
        // Arrange
        using var meters = new MeterCollector();
        var reporter = new UiFailureReporter(new CapturingLogger<UiFailureReporter>(), meters);

        // Act
        reporter.Report(new TargetInvocationException(new FormatException()), "op");

        // Assert
        meters.Measurements.ShouldHaveSingleItem().Tags[UiFailureReporter.ErrorTypeTag].ShouldBe(nameof(FormatException));
    }

    [Fact]
    public void Throwing_meter_factory_cannot_break_reporting_and_the_log_is_still_written()
    {
        // Arrange
        var logger = new CapturingLogger<UiFailureReporter>();
        using var meters = new ThrowingMeterFactory();
        var reporter = new UiFailureReporter(logger, meters);

        // Act
        var failure = Record.Exception(() => reporter.Report(new InvalidOperationException(), "customers.import"));

        // Assert
        failure.ShouldBeNull();
        logger.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(UiFailureReporter.HandledFailureEventId);
    }

    [Fact]
    public void Throwing_listener_cannot_break_reporting_and_the_log_is_still_written()
    {
        // Arrange
        var logger = new CapturingLogger<UiFailureReporter>();
        using var meters = new MeterCollector { ThrowOnMeasurement = true };
        var reporter = new UiFailureReporter(logger, meters);

        // Act
        var failure = Record.Exception(() => reporter.Report(new InvalidOperationException(), "customers.import"));

        // Assert
        failure.ShouldBeNull();
        logger.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(UiFailureReporter.HandledFailureEventId);
    }

    [Fact]
    public void Constructor_rejects_null_arguments()
    {
        // Arrange
        using var meters = new MeterCollector();

        // Act
        var nullLogger = () => new UiFailureReporter(null!, meters);
        var nullFactory = () => new UiFailureReporter(new CapturingLogger<UiFailureReporter>(), null!);

        // Assert
        nullLogger.ShouldThrow<ArgumentNullException>();
        nullFactory.ShouldThrow<ArgumentNullException>();
    }
}
