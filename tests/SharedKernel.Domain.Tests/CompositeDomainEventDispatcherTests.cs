namespace SharedKernel.Domain.Tests;

public sealed class CompositeDomainEventDispatcherTests
{
    [Fact]
    public async Task Dispatch_invokes_registered_handlers_in_order()
    {
        // Arrange
        var calls = new List<string>();
        var dispatcher = new CompositeDomainEventDispatcher(
        [
            new CapturingDomainEventDispatchHandler("audit", calls),
            new CapturingDomainEventDispatchHandler("integration", calls),
        ]);

        // Act
        await dispatcher.Dispatch(new TestDomainEvent("document-finalized"), CancellationToken.None);

        // Assert
        calls.ShouldHaveCount(2);
        calls[0].ShouldBe("audit");
        calls[1].ShouldBe("integration");
    }

    [Fact]
    public async Task Dispatch_forwards_the_cancellation_token_to_each_handler()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var first = new CancellationCapturingDomainEventDispatchHandler();
        var second = new CancellationCapturingDomainEventDispatchHandler();
        var dispatcher = new CompositeDomainEventDispatcher([first, second]);

        // Act
        await dispatcher.Dispatch(new TestDomainEvent("document-finalized"), cancellationTokenSource.Token);

        // Assert
        first.CapturedToken.ShouldBe(cancellationTokenSource.Token);
        second.CapturedToken.ShouldBe(cancellationTokenSource.Token);
    }

    [Fact]
    public async Task Dispatch_invokes_registered_handlers_for_untyped_events()
    {
        // Arrange
        var calls = new List<string>();
        var dispatcher = new CompositeDomainEventDispatcher(
        [
            new CapturingDomainEventDispatchHandler("audit", calls),
        ]);
        Domain.IDomainEvent domainEvent = new TestDomainEvent("document-finalized");

        // Act
        await dispatcher.Dispatch(domainEvent, CancellationToken.None);

        // Assert
        calls.ShouldHaveSingleItem().ShouldBe("audit");
    }

    [Fact]
    public async Task Dispatch_stops_after_the_first_handler_failure()
    {
        // Arrange
        var calls = new List<string>();
        var dispatcher = new CompositeDomainEventDispatcher(
        [
            new CapturingDomainEventDispatchHandler("before", calls),
            new ThrowingDomainEventDispatchHandler(),
            new CapturingDomainEventDispatchHandler("after", calls),
        ]);
        Func<Task> dispatch = async () =>
            await dispatcher.Dispatch(new TestDomainEvent("document-finalized"), TestContext.Current.CancellationToken);

        // Act
        var exception = await dispatch.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("The test handler rejected the domain event.");
        calls.ShouldHaveSingleItem().ShouldBe("before");
    }

    [Fact]
    public void Constructor_rejects_null_handlers()
    {
        // Arrange
        Action create = () => _ = new CompositeDomainEventDispatcher(null!);

        // Act
        var exception = create.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("handlers");
    }

    [Fact]
    public async Task Dispatch_rejects_a_null_domain_event()
    {
        // Arrange
        var dispatcher = new CompositeDomainEventDispatcher([]);
        Func<Task> dispatch = async () =>
            await dispatcher.Dispatch((Domain.IDomainEvent)null!, TestContext.Current.CancellationToken);

        // Act
        var exception = await dispatch.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("domainEvent");
    }

    [Fact]
    public async Task Dispatch_rejects_a_null_typed_domain_event()
    {
        // Arrange
        var dispatcher = new CompositeDomainEventDispatcher([]);
        Func<Task> dispatch = async () =>
            await dispatcher.Dispatch<TestDomainEvent>(null!, TestContext.Current.CancellationToken);

        // Act
        var exception = await dispatch.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("domainEvent");
    }
}
