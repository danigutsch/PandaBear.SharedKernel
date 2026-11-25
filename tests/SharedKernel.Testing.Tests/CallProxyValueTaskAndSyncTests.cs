namespace SharedKernel.Testing.Tests;

/// <summary>Tests the proxy for methods that return a value task and for synchronous methods.</summary>
public sealed class CallProxyValueTaskAndSyncTests
{
    [Fact]
    public async Task A_value_task_with_a_result_passes_through_and_is_followed_up()
    {
        // Arrange
        var seen = 0;
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Thrice.After(call => seen = call.Result));

        // Act
        var value = await proxy.Thrice(4);

        // Assert
        value.ShouldBe(12);
        seen.ShouldBe(12);
    }

    [Fact]
    public async Task A_value_task_with_a_result_can_be_replaced_without_calling_the_inner_service()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Thrice.Returns(7));

        // Act
        var value = await proxy.Thrice(1);

        // Assert
        value.ShouldBe(7);
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_value_task_without_a_result_is_held_by_a_pending_before_hook()
    {
        // Arrange
        var inner = new CallProbe();
        var release = new TaskCompletionSource();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Pause.Before(_ => release.Task));

        // Act
        var pending = proxy.Pause();
        var callsWhileHeld = inner.Calls.Count;
        release.SetResult();
        await pending;

        // Assert
        callsWhileHeld.ShouldBe(0);
        inner.Calls.ShouldBe([nameof(CallProbe.Pause)]);
    }

    [Fact]
    public async Task A_before_hook_that_throws_stops_a_value_task_call()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Thrice.Before(_ => throw new InvalidOperationException("blocked")));

        // Act
        Func<Task> act = async () => await proxy.Thrice(1);

        // Assert
        var failure = await act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("blocked");
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void A_synchronous_method_runs_its_hooks_and_returns_the_inner_result()
    {
        // Arrange
        var order = new List<string>();
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Add
            .Before(call => order.Add($"before {call.Left}+{call.Right}"))
            .After(async call =>
            {
                await Task.Yield();
                order.Add($"after {call.Result}");
            }));

        // Act
        var sum = proxy.Add(2, 3);

        // Assert
        sum.ShouldBe(5);
        order.ShouldBe(["before 2+3", "after 5"]);
    }

    [Fact]
    public void A_synchronous_method_result_can_be_replaced()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Add.Returns(100));

        // Act
        var sum = proxy.Add(1, 1);

        // Assert
        sum.ShouldBe(100);
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void A_void_method_runs_its_hooks_around_the_inner_call()
    {
        // Arrange
        var inner = new CallProbe();
        var order = new List<string>();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Touch
            .Before(_ => order.Add($"before {inner.Calls.Count}"))
            .After(_ => order.Add($"after {inner.Calls.Count}")));

        // Act
        proxy.Touch();

        // Assert
        order.ShouldBe(["before 0", "after 1"]);
    }

    [Fact]
    public void A_before_hook_that_throws_stops_a_synchronous_call_with_the_original_exception()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Touch.Before(_ => Task.FromException(new InvalidOperationException("blocked"))));

        // Act
        Action act = () => proxy.Touch();

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("blocked");
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void An_exception_of_a_synchronous_inner_service_arrives_unwrapped()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe());

        // Act
        Action act = () => proxy.Break();

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("inner failure");
    }
}
