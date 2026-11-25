using SharedKernel.Aspire.Hosting.Umami.Testing;

namespace SharedKernel.Aspire.Hosting.Umami.Tests;

/// <summary>Tests what the owner of the shared Umami hands over when the website is set up and when Umami does not start.</summary>
public sealed class UmamiTestWebsiteHandOverTests : IDisposable
{
    private readonly InvariantStringWriter error = new();

    [Fact]
    public async Task A_website_that_is_set_up_is_handed_over_with_its_connection_string_and_nothing_is_reported()
    {
        // Act
        var value = await UmamiTestWebsite.HandOver(_ => Task.FromResult(UmamiTestEnvironment.Website), error, TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe(UmamiTestEnvironment.Website);
        error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task An_umami_that_does_not_start_is_handed_over_as_unavailable_with_the_reason_on_standard_error()
    {
        // Arrange
        var failure = new TimeoutException("Resources were not healthy within 00:05:00.\n- site: FailedToStart, health unknown; last log lines:");

        // Act
        var value = await UmamiTestWebsite.HandOver(_ => Task.FromException<string>(failure), error, TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe(UmamiTestWebsite.Unavailable, "the other services are still handed over, and only the tests that borrow Umami fail");
        var report = error.ToString();
        report.ShouldStartWith(
            "Umami did not start, so SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING is Unavailable: the tests that borrow Umami fail, the other services are handed over.",
            StringComparison.Ordinal);
        report.ShouldContain("site: FailedToStart", StringComparison.Ordinal, "the report says why, so the failing tests can point at it");
    }

    [Fact]
    public async Task A_website_without_a_website_connection_string_is_handed_over_as_unavailable()
    {
        // Act
        var value = await UmamiTestWebsite.HandOver(_ => Task.FromResult("http://localhost:3000"), error, TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe(UmamiTestWebsite.Unavailable);
        error.ToString().ShouldContain("Umami did not start", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stop_requested_while_umami_starts_is_no_failure_of_umami_and_propagates()
    {
        // Arrange
        var stopped = new CancellationToken(canceled: true);

        // Act
        Func<Task> handOver = () => UmamiTestWebsite.HandOver(static ct => Task.FromCanceled<string>(ct), error, stopped);

        // Assert
        _ = await handOver.ShouldThrowAssignableTo<OperationCanceledException>();
        error.ToString().ShouldBeEmpty("a stop of the owner is reported by the owner, not as a failure of Umami");
    }

    /// <inheritdoc />
    public void Dispose() => error.Dispose();
}
