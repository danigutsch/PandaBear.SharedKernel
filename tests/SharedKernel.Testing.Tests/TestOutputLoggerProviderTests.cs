using Microsoft.Extensions.Logging;

namespace PandaBear.Testing.Tests;

/// <summary>
/// Tests that log entries go to the output of the xUnit test they belong to, which the runner shows only for a failing test, instead of
/// the process console, which <c>dotnet test</c> streams for every test.
/// </summary>
public sealed class TestOutputLoggerProviderTests
{
    [Fact]
    public void An_entry_goes_to_the_output_of_the_running_test()
    {
        // Arrange
        using var provider = new TestOutputLoggerProvider();
        var logger = provider.CreateLogger("Orders.Checkout");

        // Act
        SampleLogs.OrderLate(logger, 42);

        // Assert
        TestContext.Current.TestOutputHelper.ShouldNotBeNull().Output.ShouldContain("warn: Orders.Checkout[7] Order 42 is late", StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_keeps_its_exception()
    {
        // Arrange
        using var provider = new TestOutputLoggerProvider();
        var logger = provider.CreateLogger("Payments");

        // Act
        SampleLogs.PaymentFailed(logger, new InvalidOperationException("card declined"));

        // Assert
        var output = TestContext.Current.TestOutputHelper.ShouldNotBeNull().Output;
        output.ShouldContain("fail: Payments[0] Payment failed", StringComparison.Ordinal);
        output.ShouldContain("System.InvalidOperationException: card declined", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entry_logged_outside_any_test_goes_to_the_test_that_created_the_provider()
    {
        // Arrange
        using var provider = new TestOutputLoggerProvider();
        var logger = provider.CreateLogger("Server.Requests");

        // Act
        await OutsideAnyTest.Run(() => SampleLogs.Request(logger, "/ping"));

        // Assert
        TestContext.Current.TestOutputHelper.ShouldNotBeNull().Output.ShouldContain("info: Server.Requests[0] GET /ping", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entry_logged_in_a_test_goes_to_that_test_even_when_the_provider_was_created_outside_any_test()
    {
        // Arrange
        TestOutputLoggerProvider? provider = null;
        await OutsideAnyTest.Run(() => provider = new TestOutputLoggerProvider());
        using var shared = provider.ShouldNotBeNull();
        var logger = shared.CreateLogger("Shared.Host");

        // Act
        SampleLogs.Request(logger, "/orders");

        // Assert
        TestContext.Current.TestOutputHelper.ShouldNotBeNull().Output.ShouldContain("info: Shared.Host[0] GET /orders", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entry_that_belongs_to_no_running_test_is_not_written_and_does_not_throw()
    {
        // Arrange
        using var provider = new TestOutputLoggerProvider(new Xunit.v3.TestOutputHelper());
        var logger = provider.CreateLogger("After.The.Test");

        // Act
        await OutsideAnyTest.Run(() => SampleLogs.Request(logger, "/late"));

        // Assert
        TestContext.Current.TestOutputHelper.ShouldNotBeNull().Output.ShouldNotContain("After.The.Test", StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_is_enabled_at_level_none()
    {
        // Arrange
        using var provider = new TestOutputLoggerProvider();

        // Act
        var logger = provider.CreateLogger("Any");

        // Assert
        logger.IsEnabled(LogLevel.None).ShouldBeFalse();
        logger.IsEnabled(LogLevel.Trace).ShouldBeTrue();
    }
}
