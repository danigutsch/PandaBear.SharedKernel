using System.Collections.Concurrent;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

public sealed class OnDemandResourceTests
{
    [Fact]
    public async Task Concurrent_first_callers_start_the_resource_once_and_share_the_value()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var shared = new object();
        var start = new ControlledStart<object>();
        await using var resource = new OnDemandResource<object>(start.Start);

        // Act
        var callers = Enumerable.Range(0, 16).Select(_ => Task.Run(() => resource.Get(ct), ct)).ToArray();
        start.Complete(shared);
        var values = await Task.WhenAll(callers);

        // Assert
        start.Calls.ShouldBe(1, "every concurrent caller must share the first start");
        values.ShouldAllSatisfy(value => value.ShouldBeSameAs(shared), "every caller must receive the shared value");
    }

    [Fact]
    public async Task Later_callers_share_the_started_value_without_starting_again()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var shared = new object();
        var start = new ControlledStart<object>();
        await using var resource = new OnDemandResource<object>(start.Start);
        start.Complete(shared);
        var first = await resource.Get(ct);

        // Act
        var later = await resource.Get(ct);

        // Assert
        first.ShouldBeSameAs(shared);
        later.ShouldBeSameAs(shared, "a later caller must receive the value the first caller started");
        start.Calls.ShouldBe(1, "a started resource must not start again");
    }

    [Fact]
    public async Task Start_runs_synchronously_on_the_first_callers_thread()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<string>();
        await using var resource = new OnDemandResource<string>(start.Start);
        var callerThreadId = Environment.CurrentManagedThreadId;

        // Act
        var pending = resource.Get(ct);
        var callsWhenGetReturned = start.Calls;
        var startThreadId = start.ThreadId;
        start.Complete("value");
        var value = await pending;

        // Assert
        callsWhenGetReturned.ShouldBe(1, "Aspire reads its metadata from the synchronous call stack of the start delegate");
        startThreadId.ShouldBe(callerThreadId, "the start delegate must not move to another thread");
        value.ShouldBe("value");
    }

    [Fact]
    public async Task Caller_cancellation_abandons_only_that_wait_and_never_cancels_the_start()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<string>();
        await using var resource = new OnDemandResource<string>(start.Start);
        using var impatientCaller = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var abandoned = resource.Get(impatientCaller.Token);
        var patient = resource.Get(ct);

        // Act
        await impatientCaller.CancelAsync();
        Func<Task> awaitAbandoned = () => abandoned;
        _ = await awaitAbandoned.ShouldThrowAssignableTo<OperationCanceledException>();
        var startTokenCancelled = start.Token.IsCancellationRequested;
        start.Complete("value");
        var patientValue = await patient;
        var laterValue = await resource.Get(ct);

        // Assert
        start.Token.ShouldNotBe(impatientCaller.Token, "the start must run with a token owned by the resource");
        startTokenCancelled.ShouldBeFalse("a caller token only abandons that caller's wait");
        patientValue.ShouldBe("value");
        laterValue.ShouldBe("value");
        start.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Cancelled_caller_token_throws_without_starting()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<string>();
        await using var resource = new OnDemandResource<string>(start.Start);
        using var cancelledCaller = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await cancelledCaller.CancelAsync();

        // Act
        Func<Task> get = () => resource.Get(cancelledCaller.Token);
        _ = await get.ShouldThrowAssignableTo<OperationCanceledException>();
        var callsAfterCancelledGet = start.Calls;
        start.Complete("value");
        var value = await resource.Get(ct);

        // Assert
        callsAfterCancelledGet.ShouldBe(0, "a caller that is already cancelled must not start the resource");
        value.ShouldBe("value");
        start.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Start_failure_is_cached_and_never_repeated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("startup failed");
        var start = new ControlledStart<string>();
        await using var resource = new OnDemandResource<string>(start.Start);
        var firstCaller = resource.Get(ct);
        var secondCaller = resource.Get(ct);

        // Act
        start.Fail(failure);
        Func<Task> awaitFirst = () => firstCaller;
        Func<Task> awaitSecond = () => secondCaller;
        Func<Task> awaitLater = () => resource.Get(ct);
        var firstFailure = await awaitFirst.ShouldThrow<InvalidOperationException>();
        var secondFailure = await awaitSecond.ShouldThrow<InvalidOperationException>();
        var laterFailure = await awaitLater.ShouldThrow<InvalidOperationException>();

        // Assert
        firstFailure.ShouldBeSameAs(failure);
        secondFailure.ShouldBeSameAs(failure, "concurrent callers must observe the same failure");
        laterFailure.ShouldBeSameAs(failure, "a later caller must observe the cached failure instead of retrying");
        start.Calls.ShouldBe(1, "a failed start must not repeat a multi-minute startup timeout");
    }

    [Fact]
    public async Task Synchronous_start_failure_becomes_a_cached_faulted_task()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("startup failed");
        var calls = 0;
        await using var resource = new OnDemandResource<string>(_ =>
        {
            calls++;
            throw failure;
        });

        // Act
        var first = resource.Get(ct);
        var second = resource.Get(ct);
        Func<Task> awaitFirst = () => first;
        Func<Task> awaitSecond = () => second;
        var firstFailure = await awaitFirst.ShouldThrow<InvalidOperationException>();
        var secondFailure = await awaitSecond.ShouldThrow<InvalidOperationException>();

        // Assert
        first.IsFaulted.ShouldBeTrue("a synchronous throw must be reported through the task");
        firstFailure.ShouldBeSameAs(failure);
        secondFailure.ShouldBeSameAs(failure);
        calls.ShouldBe(1, "the synchronous failure must be cached too");
    }

    [Fact]
    public async Task Is_started_is_true_only_after_the_start_succeeded_and_until_disposal()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<string>();
        var resource = new OnDemandResource<string>(start.Start);
        var beforeStart = resource.IsStarted;

        // Act
        var pending = resource.Get(ct);
        var whilePending = resource.IsStarted;
        start.Complete("value");
        _ = await pending;
        var afterStart = resource.IsStarted;
        await resource.DisposeAsync();
        var afterDisposal = resource.IsStarted;

        // Assert
        beforeStart.ShouldBeFalse();
        whilePending.ShouldBeFalse("a pending start has not produced a usable value");
        afterStart.ShouldBeTrue();
        afterDisposal.ShouldBeFalse("a disposed resource no longer holds a started value");
    }

    [Fact]
    public async Task Is_started_stays_false_after_a_failed_start()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var resource = new OnDemandResource<string>(_ => Task.FromException<string>(new InvalidOperationException("startup failed")));
        Func<Task> get = () => resource.Get(ct);

        // Act
        _ = await get.ShouldThrow<InvalidOperationException>();
        var isStarted = resource.IsStarted;

        // Assert
        isStarted.ShouldBeFalse();
    }

    [Fact]
    public async Task Disposing_an_unstarted_resource_never_starts_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<string>();
        var stops = 0;
        var resource = new OnDemandResource<string>(start.Start, _ =>
        {
            stops++;
            return ValueTask.CompletedTask;
        });

        // Act
        // A disposal that starts the resource and waits for it must fail this test instead of hanging it.
        await resource.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), ct);
        Func<Task> get = () => resource.Get(ct);
        _ = await get.ShouldThrow<ObjectDisposedException>();

        // Assert
        start.Calls.ShouldBe(0, "disposal and a later Get must not start the resource");
        stops.ShouldBe(0, "there is nothing to stop");
        resource.IsStarted.ShouldBeFalse();
    }

    [Fact]
    public async Task Disposing_during_a_pending_start_cancels_it_and_swallows_the_cancellation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<AsyncDisposableMarker>();
        var stops = 0;
        var resource = new OnDemandResource<AsyncDisposableMarker>(start.Start, _ =>
        {
            stops++;
            return ValueTask.CompletedTask;
        });
        var pending = resource.Get(ct);

        // Act
        var disposal = resource.DisposeAsync().AsTask();
        await start.TokenCancelled;
        start.Abandon();
        await disposal;
        Func<Task> awaitPending = () => pending;
        _ = await awaitPending.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        stops.ShouldBe(0, "a cancelled start produced no value to stop");
        resource.IsStarted.ShouldBeFalse();
    }

    [Fact]
    public async Task Disposing_during_a_pending_start_releases_the_value_the_start_produces()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<AsyncDisposableMarker>();
        var produced = new AsyncDisposableMarker();
        var resource = new OnDemandResource<AsyncDisposableMarker>(start.Start);
        var pending = resource.Get(ct);

        // Act
        var disposal = resource.DisposeAsync().AsTask();
        await start.TokenCancelled;
        var finishedBeforeStart = disposal.IsCompleted;
        start.Complete(produced);
        await disposal;
        _ = await pending;

        // Assert
        finishedBeforeStart.ShouldBeFalse("disposal must wait for the pending start before it can release its value");
        produced.DisposeCount.ShouldBe(1, "a value produced after disposal began must still be disposed once");
    }

    [Fact]
    public async Task Disposing_during_a_pending_start_surfaces_a_startup_failure_that_is_not_the_requested_cancellation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("startup and cleanup failed");
        var start = new ControlledStart<string>();
        var resource = new OnDemandResource<string>(start.Start);
        var pending = resource.Get(ct);
        var disposal = resource.DisposeAsync().AsTask();
        await start.TokenCancelled;

        // Act
        start.Fail(failure);
        Func<Task> awaitDisposal = () => disposal;
        Func<Task> awaitPending = () => pending;
        var surfaced = await awaitDisposal.ShouldThrow<InvalidOperationException>();
        var seenByCaller = await awaitPending.ShouldThrow<InvalidOperationException>();

        // Assert
        surfaced.ShouldBeSameAs(failure, "a failure that carries cleanup errors must not be masked by disposal");
        seenByCaller.ShouldBeSameAs(failure, "the caller of Get still observes the failure");
    }

    [Fact]
    public async Task Disposing_a_started_resource_stops_the_value_once()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var stopped = new List<string>();
        var resource = new OnDemandResource<string>(
            _ => Task.FromResult("value"),
            value =>
            {
                stopped.Add(value);
                return ValueTask.CompletedTask;
            });
        _ = await resource.Get(ct);

        // Act
        await resource.DisposeAsync();
        await resource.DisposeAsync();

        // Assert
        stopped.ShouldBe(["value"], "the stop delegate runs once with the started value");
    }

    [Fact]
    public async Task Disposing_a_started_resource_disposes_an_async_disposable_value_by_default()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var resource = new OnDemandResource<AsyncDisposableMarker>(_ => Task.FromResult(new AsyncDisposableMarker()));
        var marker = await resource.Get(ct);

        // Act
        await resource.DisposeAsync();
        await resource.DisposeAsync();

        // Assert
        marker.DisposeCount.ShouldBe(1, "the started value is disposed once, however often the resource is disposed");
    }

    [Fact]
    public async Task Disposing_a_started_resource_disposes_a_disposable_value_by_default()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var resource = new OnDemandResource<DisposableMarker>(_ => Task.FromResult(new DisposableMarker()));
        var marker = await resource.Get(ct);

        // Act
        await resource.DisposeAsync();
        await resource.DisposeAsync();

        // Assert
        marker.DisposeCount.ShouldBe(1, "the started value is disposed once, however often the resource is disposed");
    }

    [Fact]
    public async Task A_stop_delegate_replaces_the_default_disposal_of_the_value()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var stops = 0;
        var resource = new OnDemandResource<AsyncDisposableMarker>(
            _ => Task.FromResult(new AsyncDisposableMarker()),
            _ =>
            {
                stops++;
                return ValueTask.CompletedTask;
            });
        var marker = await resource.Get(ct);

        // Act
        await resource.DisposeAsync();

        // Assert
        stops.ShouldBe(1);
        marker.DisposeCount.ShouldBe(0, "the stop delegate owns teardown when it is supplied");
    }

    [Fact]
    public async Task Disposing_after_a_failed_start_has_nothing_to_release()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var stops = 0;
        var resource = new OnDemandResource<string>(
            _ => Task.FromException<string>(new InvalidOperationException("startup failed")),
            _ =>
            {
                stops++;
                return ValueTask.CompletedTask;
            });
        Func<Task> get = () => resource.Get(ct);
        _ = await get.ShouldThrow<InvalidOperationException>();

        // Act
        await resource.DisposeAsync();

        // Assert
        stops.ShouldBe(0, "a failed start produced no value, and its failure belongs to the callers of Get");
    }

    [Fact]
    public async Task A_failing_stop_delegate_surfaces_from_disposal_and_runs_once()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("stop failed");
        var stops = 0;
        var resource = new OnDemandResource<string>(
            _ => Task.FromResult("value"),
            _ =>
            {
                stops++;
                return ValueTask.FromException(failure);
            });
        _ = await resource.Get(ct);
        Func<Task> dispose = () => resource.DisposeAsync().AsTask();

        // Act
        var surfaced = await dispose.ShouldThrow<InvalidOperationException>();
        await resource.DisposeAsync();

        // Assert
        surfaced.ShouldBeSameAs(failure);
        stops.ShouldBe(1, "a second disposal must not stop the value again");
    }

    [Fact]
    public async Task A_failing_async_disposal_of_the_value_surfaces_from_disposal()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("dispose failed");
        await using var resource = new OnDemandResource<AsyncDisposableMarker>(_ => Task.FromResult(new AsyncDisposableMarker(failure)));
        var marker = await resource.Get(ct);
        Func<Task> dispose = () => resource.DisposeAsync().AsTask();

        // Act
        var surfaced = await dispose.ShouldThrow<InvalidOperationException>();

        // Assert
        surfaced.ShouldBeSameAs(failure);
        marker.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_failing_disposal_of_the_value_surfaces_from_disposal()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("dispose failed");
        await using var resource = new OnDemandResource<DisposableMarker>(_ => Task.FromResult(new DisposableMarker(failure)));
        var marker = await resource.Get(ct);
        Func<Task> dispose = () => resource.DisposeAsync().AsTask();

        // Act
        var surfaced = await dispose.ShouldThrow<InvalidOperationException>();

        // Assert
        surfaced.ShouldBeSameAs(failure);
        marker.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task Get_after_disposal_throws_without_starting_again()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<string>();
        var resource = new OnDemandResource<string>(start.Start);
        start.Complete("value");
        _ = await resource.Get(ct);
        await resource.DisposeAsync();

        // Act
        Func<Task> get = () => resource.Get(ct);
        _ = await get.ShouldThrow<ObjectDisposedException>();

        // Assert
        start.Calls.ShouldBe(1, "a disposed resource must never start again");
    }

    [Fact]
    public async Task Value_types_and_values_without_teardown_need_no_stop_delegate()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var number = new OnDemandResource<int>(_ => Task.FromResult(42));
        var endpoint = new OnDemandResource<Uri>(_ => Task.FromResult(new Uri("http://localhost:5432")));

        // Act
        var numberValue = await number.Get(ct);
        var endpointValue = await endpoint.Get(ct);
        await number.DisposeAsync();
        await endpoint.DisposeAsync();

        // Assert
        numberValue.ShouldBe(42);
        endpointValue.ShouldBe(new Uri("http://localhost:5432"));
    }

    [Fact]
    public async Task Racing_get_and_disposal_never_leaves_a_started_value_undisposed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var markers = new ConcurrentBag<AsyncDisposableMarker>();
        var resources = Enumerable.Range(0, 64).Select(_ => new OnDemandResource<AsyncDisposableMarker>(token =>
        {
            var marker = new AsyncDisposableMarker();
            markers.Add(marker);
            return Task.FromResult(marker);
        })).ToArray();

        // Act
        var races = resources.Select(resource => Task.WhenAll(
            Task.Run(() => resource.Get(ct), ct),
            Task.Run(() => resource.DisposeAsync().AsTask(), ct))).ToArray();
        _ = await Task.WhenAny(Task.WhenAll(races));
        var failures = races.Where(race => race.IsFaulted).SelectMany(race => race.Exception!.InnerExceptions).ToArray();

        // Assert
        markers.Count.ShouldBeLessThanOrEqualTo(resources.Length, "no resource may start twice");
        markers.ShouldAllSatisfy(marker => marker.DisposeCount.ShouldBe(1, "a started value must be disposed exactly once"));
        failures.ShouldAllSatisfy(failure => failure.ShouldBeOfType<ObjectDisposedException>(), "only a Get that lost the race may fail");
    }

    [Fact]
    public void A_start_delegate_is_required()
    {
        // Arrange
        Func<object> construct = () => new OnDemandResource<string>(null!);

        // Act, Assert
        _ = construct.ShouldThrow<ArgumentNullException>();
    }
}
