using System.Collections.Concurrent;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>Tests OnDemandResource, which starts a value on first demand, shares or caches the outcome, and releases the value on disposal.</summary>
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

        // Disposed explicitly below: disposal waits for the start, which only completes after the wait that follows.
        var resource = new OnDemandResource<string>(start.Start);
        using var impatientCaller = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var abandoned = resource.Get(impatientCaller.Token);
        var patient = resource.Get(ct);

        // Act
        await impatientCaller.CancelAsync();

        // The start completes only after this wait, so a caller that is not abandoned must fail the test, not hang it.
        Func<Task> awaitAbandoned = () => abandoned.WaitAsync(TimeSpan.FromSeconds(30));
        _ = await awaitAbandoned.ShouldThrowAssignableTo<OperationCanceledException>();
        var startTokenCancelled = start.Token.IsCancellationRequested;
        start.Complete("value");
        var patientValue = await patient;
        var laterValue = await resource.Get(ct);
        await resource.DisposeAsync();

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
    public async Task Disposing_an_unstarted_resource_never_starts_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<AsyncDisposableMarker>();
        var resource = new OnDemandResource<AsyncDisposableMarker>(start.Start);

        // Act
        // A disposal that starts the resource and waits for it must fail this test instead of hanging it.
        await resource.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), ct);
        Func<Task> get = () => resource.Get(ct);
        _ = await get.ShouldThrow<ObjectDisposedException>();

        // Assert
        start.Calls.ShouldBe(0, "disposal and a later Get must not start the resource");
    }

    [Fact]
    public async Task Disposing_during_a_pending_start_cancels_it_and_swallows_the_cancellation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var start = new ControlledStart<AsyncDisposableMarker>();
        var resource = new OnDemandResource<AsyncDisposableMarker>(start.Start);
        var pending = resource.Get(ct);

        // Act
        var disposal = resource.DisposeAsync().AsTask();
        await start.TokenCancelled;
        start.Abandon();
        await disposal;
        Func<Task> awaitPending = () => pending;
        _ = await awaitPending.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        disposal.Status.ShouldBe(TaskStatus.RanToCompletion, "the cancellation that disposal requested is not a failure of disposal");
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
    public async Task Disposing_a_started_resource_disposes_an_async_disposable_value()
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
    public async Task Disposing_a_started_resource_disposes_a_disposable_value()
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
    public async Task Disposing_after_a_failed_start_has_nothing_to_release()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var resource = new OnDemandResource<AsyncDisposableMarker>(
            _ => Task.FromException<AsyncDisposableMarker>(new InvalidOperationException("startup failed")));
        Func<Task> get = () => resource.Get(ct);
        _ = await get.ShouldThrow<InvalidOperationException>();

        // Act
        var disposal = resource.DisposeAsync().AsTask();
        await disposal.WaitAsync(TimeSpan.FromSeconds(30), ct);

        // Assert
        disposal.Status.ShouldBe(TaskStatus.RanToCompletion, "a failed start produced no value, and its failure belongs to the callers of Get");
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
        await resource.DisposeAsync();

        // Assert
        surfaced.ShouldBeSameAs(failure);
        marker.DisposeCount.ShouldBe(1, "a second disposal must neither repeat the failure nor dispose the value again");
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
    public async Task Values_without_teardown_are_left_alone_on_disposal()
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
    public async Task Racing_gets_and_disposal_start_each_resource_at_most_once_and_never_leave_a_started_value_undisposed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var starts = new ConcurrentBag<(int Resource, AsyncDisposableMarker Marker)>();
        var resources = Enumerable.Range(0, 64).Select(index => new OnDemandResource<AsyncDisposableMarker>(token =>
        {
            var marker = new AsyncDisposableMarker();
            starts.Add((index, marker));
            return Task.FromResult(marker);
        })).ToArray();

        // Act
        var races = resources.Select(resource => Task.WhenAll(
            Task.Run(() => resource.Get(ct), ct),
            Task.Run(() => resource.Get(ct), ct),
            Task.Run(() => resource.DisposeAsync().AsTask(), ct))).ToArray();
        _ = await Task.WhenAny(Task.WhenAll(races));
        var failures = races.Where(race => race.IsFaulted).SelectMany(race => race.Exception!.InnerExceptions).ToArray();

        // Assert
        starts.GroupBy(start => start.Resource).ShouldAllSatisfy(
            resourceStarts => resourceStarts.Count().ShouldBe(1, "concurrent callers must share one start"));
        starts.ShouldAllSatisfy(start => start.Marker.DisposeCount.ShouldBe(1, "a started value must be disposed exactly once"));
        failures.ShouldAllSatisfy(failure => failure.ShouldBeOfType<ObjectDisposedException>(), "only a Get that lost the race may fail");
    }

    [Fact]
    public async Task A_throwing_cancellation_callback_does_not_let_disposal_abandon_the_pending_start()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var callbackFailure = new InvalidOperationException("cancellation callback failed");
        var produced = new AsyncDisposableMarker();
        var start = new ControlledStart<AsyncDisposableMarker>(callbackFailure);
        var resource = new OnDemandResource<AsyncDisposableMarker>(start.Start);
        var pending = resource.Get(ct);
        var disposal = resource.DisposeAsync().AsTask();
        await start.TokenCancelled;

        // Act
        var finishedBeforeStart = disposal.IsCompleted;
        start.Complete(produced);
        Func<Task> awaitDisposal = () => disposal;
        var surfaced = await awaitDisposal.ShouldThrowAssignableTo<Exception>();
        _ = await pending;

        // Assert
        finishedBeforeStart.ShouldBeFalse("disposal must keep waiting for the pending start although a cancellation callback threw");
        produced.DisposeCount.ShouldBe(1, "a value produced after the callback failure must still be released");
        ExceptionLeaves.Of(surfaced).ShouldContain(callbackFailure, "the failure of the callback must not be masked");
    }

    [Fact]
    public async Task A_throwing_cancellation_callback_is_reported_together_with_a_failing_disposal_of_the_value()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var callbackFailure = new InvalidOperationException("cancellation callback failed");
        var disposeFailure = new InvalidOperationException("dispose failed");
        var start = new ControlledStart<AsyncDisposableMarker>(callbackFailure);
        var resource = new OnDemandResource<AsyncDisposableMarker>(start.Start);
        var pending = resource.Get(ct);
        var disposal = resource.DisposeAsync().AsTask();
        await start.TokenCancelled;

        // Act
        start.Complete(new AsyncDisposableMarker(disposeFailure));
        Func<Task> awaitDisposal = () => disposal;
        var surfaced = await awaitDisposal.ShouldThrowAssignableTo<Exception>();
        _ = await pending;

        // Assert
        var failures = ExceptionLeaves.Of(surfaced);
        failures.ShouldContain(callbackFailure, "the failed cancellation callback must be reported");
        failures.ShouldContain(disposeFailure, "the failing disposal of the value must be reported with it, not skipped");
    }

    [Fact]
    public async Task A_start_failure_that_no_caller_received_surfaces_from_disposal()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("startup and cleanup failed");
        var failStart = new TaskCompletionSource();

        // The start observes its token, so a failure above makes the disposal at the end cancel it instead of hanging.
        await using var resource = new OnDemandResource<string>(async token =>
        {
            await failStart.Task.WaitAsync(token).ConfigureAwait(false);
            throw failure;
        });
        using var impatientCaller = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var abandoned = resource.Get(impatientCaller.Token);
        await impatientCaller.CancelAsync();
        Func<Task> awaitAbandoned = () => abandoned.WaitAsync(TimeSpan.FromSeconds(30));
        _ = await awaitAbandoned.ShouldThrowAssignableTo<OperationCanceledException>();

        // A thread-pool thread has no synchronization context, so the continuations of the start run inline and the
        // start has failed, with nobody waiting, by the time this returns.
        await Task.Run(() => failStart.SetResult(), ct);

        // Act
        Func<Task> dispose = () => resource.DisposeAsync().AsTask();
        var surfaced = await dispose.ShouldThrow<InvalidOperationException>();

        // Assert
        surfaced.ShouldBeSameAs(failure, "a failure that every caller abandoned must not be lost");
    }

    [Fact]
    public async Task Get_from_the_synchronous_part_of_the_start_throws_instead_of_recursing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        OnDemandResource<string>? resource = null;
        resource = new OnDemandResource<string>(token => resource!.Get(token));

        // A nested call that is not rejected waits for the start it belongs to, so it must fail this test, not hang it.
        Func<Task> get = () => resource.Get(ct).WaitAsync(TimeSpan.FromSeconds(30));

        // Act
        var failure = await get.ShouldThrow<InvalidOperationException>();
        await resource.DisposeAsync();

        // Assert
        failure.Message.ShouldContain("Get", "the failure must name the forbidden call");
    }

    [Fact]
    public async Task A_start_cancelled_by_something_other_than_disposal_fails_with_a_descriptive_exception()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var startupTimeout = new CancellationTokenSource();
        await startupTimeout.CancelAsync();
        var calls = 0;
        await using var resource = new OnDemandResource<string>(_ =>
        {
            calls++;
            return Task.FromCanceled<string>(startupTimeout.Token);
        });
        Func<Task> first = () => resource.Get(ct);
        Func<Task> later = () => resource.Get(ct);

        // Act
        var firstFailure = await first.ShouldThrow<InvalidOperationException>();
        var laterFailure = await later.ShouldThrow<InvalidOperationException>();

        // Assert
        _ = firstFailure.InnerException.ShouldBeAssignableTo<OperationCanceledException>("the cancellation must stay attached as the cause");
        laterFailure.ShouldBeSameAs(firstFailure, "the failure is cached like any other");
        calls.ShouldBe(1, "a cancelled start must not repeat");
    }

    [Fact]
    public async Task A_concurrent_second_disposal_waits_for_the_first_without_repeating_its_failure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("dispose failed");
        var resource = new OnDemandResource<BlockedDisposal>(_ => Task.FromResult(new BlockedDisposal(failure)));
        var value = await resource.Get(ct);
        var first = resource.DisposeAsync().AsTask();
        await value.Entered;

        // Act
        var second = resource.DisposeAsync().AsTask();
        var secondFinishedWhileDisposalRuns = second.IsCompleted;
        value.Finish();
        Func<Task> awaitFirst = () => first;
        var surfaced = await awaitFirst.ShouldThrow<InvalidOperationException>();
        await second.WaitAsync(TimeSpan.FromSeconds(30), ct);

        // Assert
        secondFinishedWhileDisposalRuns.ShouldBeFalse("a disposal that overlaps the first must not complete before the value is disposed");
        surfaced.ShouldBeSameAs(failure, "the first disposal owns the failure");
    }

    [Fact]
    public async Task Disposing_a_value_that_is_both_async_and_sync_disposable_uses_the_async_disposal_only()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var resource = new OnDemandResource<DualDisposableMarker>(_ => Task.FromResult(new DualDisposableMarker()));
        var marker = await resource.Get(ct);

        // Act
        await resource.DisposeAsync();

        // Assert
        marker.AsyncDisposeCount.ShouldBe(1, "IAsyncDisposable is preferred when a value implements both interfaces");
        marker.DisposeCount.ShouldBe(0, "a value is disposed through one path only");
    }

    [Fact]
    public void A_start_delegate_is_required()
    {
        // Arrange
        Func<object> construct = () => new OnDemandResource<string>(null!);

        // Act
        var failure = construct.ShouldThrow<ArgumentNullException>();

        // Assert
        failure.ParamName.ShouldBe("start", "the argument name is part of the constructor contract");
    }
}
