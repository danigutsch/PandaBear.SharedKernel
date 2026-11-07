namespace PandaBear.Testing.Tests;

/// <summary>Tests the proxy that tests use to hold, fail, replace or follow up calls to a service.</summary>
public sealed class CallProxyTests
{
    [Fact]
    public async Task A_proxy_without_hooks_passes_calls_and_results_through()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner);

        // Act
        var doubled = await proxy.Twice(21);
        proxy.Touch();

        // Assert
        doubled.ShouldBe(42);
        inner.Calls.ShouldBe([nameof(CallProbe.Twice), nameof(CallProbe.Touch)]);
    }

    [Fact]
    public async Task A_before_hook_runs_before_the_inner_call_and_sees_the_arguments()
    {
        // Arrange
        var inner = new CallProbe();
        var seen = new List<string>();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Before(nameof(ICallProbe.Twice), call =>
        {
            seen.Add($"before {call.Args[0]} after {inner.Calls.Count} inner calls");
            return Task.CompletedTask;
        }));

        // Act
        await proxy.Twice(5);

        // Assert
        seen.ShouldBe(["before 5 after 0 inner calls"]);
    }

    [Fact]
    public async Task A_before_hook_that_stays_pending_holds_the_call_until_it_completes()
    {
        // Arrange
        var inner = new CallProbe();
        var release = new TaskCompletionSource();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Before(nameof(ICallProbe.Run), _ => release.Task));

        // Act
        var call = proxy.Run();
        var callsWhileHeld = inner.Calls.Count;
        release.SetResult();
        await call;

        // Assert
        callsWhileHeld.ShouldBe(0);
        inner.Calls.ShouldBe([nameof(CallProbe.Run)]);
    }

    [Fact]
    public async Task An_after_hook_sees_the_result()
    {
        // Arrange
        object? result = null;
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.After(nameof(ICallProbe.Twice), call =>
        {
            result = call.Result;
            return Task.CompletedTask;
        }));

        // Act
        await proxy.Twice(4);

        // Assert
        result.ShouldBe(8);
    }

    [Fact]
    public async Task Returns_replaces_the_result_without_calling_the_inner_service()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Returns(nameof(ICallProbe.Twice), 99));

        // Act
        var value = await proxy.Twice(1);

        // Assert
        value.ShouldBe(99);
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_hook_only_applies_to_its_method()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Before(nameof(ICallProbe.Twice), _ => throw new InvalidOperationException("held")));

        // Act
        await proxy.Run();

        // Assert
        inner.Calls.ShouldBe([nameof(CallProbe.Run)]);
    }

    [Fact]
    public async Task A_before_hook_that_throws_stops_the_call()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Before(nameof(ICallProbe.Run), _ => throw new InvalidOperationException("blocked")));

        // Act
        Func<Task> act = () => proxy.Run();

        // Assert
        var failure = await act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("blocked");
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_exception_of_the_inner_service_arrives_unwrapped()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe());

        // Act
        Func<Task> act = () => proxy.Fail();

        // Assert
        var failure = await act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("inner failure");
    }

    [Fact]
    public void A_null_inner_service_is_rejected()
    {
        // Act
        Action act = () => CallProxy.Create<ICallProbe>(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
