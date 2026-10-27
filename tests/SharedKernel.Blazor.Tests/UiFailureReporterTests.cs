using System.Diagnostics;
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
        var counter = new RecordingCounter();
        var reporter = new UiFailureReporter(logger, counter);
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
        counter.Count.ShouldBe(1);
    }

    [Fact]
    public void Report_works_without_a_counter_and_without_an_ambient_activity()
    {
        // Arrange
        var logger = new CapturingLogger<UiFailureReporter>();
        var reporter = new UiFailureReporter(logger);

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
        var reporter = new UiFailureReporter(logger);

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
        var reporter = new UiFailureReporter(logger);

        // Act
        reporter.Report(new InvalidOperationException(), HostileText.VeryLong(10_000));

        // Assert
        logger.Entries.ShouldHaveSingleItem().Values["Operation"].ShouldBe(UiFailureReporter.UnknownOperation);
    }

    [Fact]
    public void Null_exception_is_rejected()
    {
        // Arrange
        var reporter = new UiFailureReporter(new CapturingLogger<UiFailureReporter>());

        // Act
        var report = () => reporter.Report(null!, "operation");

        // Assert
        report.ShouldThrow<ArgumentNullException>();
    }
}
