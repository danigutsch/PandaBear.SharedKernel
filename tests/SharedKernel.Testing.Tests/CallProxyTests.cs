namespace PandaBear.Testing.Tests;

/// <summary>Tests the proxy that tests use to hold, fail, replace or follow up calls to a service, through its typed hooks.</summary>
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
        proxy.ShouldNotBeSameAs(inner);
        inner.Calls.ShouldBe([nameof(CallProbe.Twice), nameof(CallProbe.Touch)]);
    }

    [Fact]
    public async Task A_before_hook_runs_before_the_inner_call_and_sees_the_typed_arguments()
    {
        // Arrange
        var inner = new CallProbe();
        var seen = new List<string>();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Twice.Before(call => seen.Add($"before {call.Value * 10} after {inner.Calls.Count} inner calls")));

        // Act
        await proxy.Twice(5);

        // Assert
        seen.ShouldBe(["before 50 after 0 inner calls"]);
    }

    [Fact]
    public async Task A_before_hook_that_stays_pending_holds_the_call_until_it_completes()
    {
        // Arrange
        var inner = new CallProbe();
        var release = new TaskCompletionSource();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Run.Before(_ => release.Task));

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
    public async Task An_after_hook_sees_the_typed_result_and_arguments()
    {
        // Arrange
        var seen = new List<int>();
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Twice.After(call =>
        {
            seen.Add(call.Value);
            seen.Add(call.Result);
        }));

        // Act
        await proxy.Twice(4);

        // Assert
        seen.ShouldBe([4, 8]);
    }

    [Fact]
    public async Task Returns_replaces_the_result_without_calling_the_inner_service()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Twice.Returns(99));

        // Act
        var value = await proxy.Twice(1);

        // Assert
        value.ShouldBe(99);
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Returns_runs_the_hooks_around_the_replaced_result()
    {
        // Arrange
        var order = new List<string>();
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Twice
            .Returns(7)
            .Before(call => order.Add($"before {call.Value}"))
            .After(call => order.Add($"after {call.Result}")));

        // Act
        await proxy.Twice(3);

        // Assert
        order.ShouldBe(["before 3", "after 7"]);
    }

    [Fact]
    public async Task The_last_replacement_wins()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Twice.Returns(1).Returns(2));

        // Act
        var value = await proxy.Twice(1);

        // Assert
        value.ShouldBe(2);
    }

    [Fact]
    public async Task A_null_replacement_for_a_nullable_result_is_returned()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks =>
        {
            hooks.FindName.Returns(null);
            hooks.FindNumber.Returns(null);
        });

        // Act
        var name = await proxy.FindName();
        var number = await proxy.FindNumber();

        // Assert
        name.ShouldBeNull();
        number.ShouldBeNull();
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Hooks_of_one_method_run_in_the_order_they_were_added()
    {
        // Arrange
        var order = new List<string>();
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks =>
        {
            hooks.Run.Before(_ => order.Add("first"));
            hooks.Run.Before(async _ =>
            {
                await Task.Yield();
                order.Add("second");
            });
            hooks.Run.Before(_ => order.Add("third"));
        });

        // Act
        await proxy.Run();

        // Assert
        order.ShouldBe(["first", "second", "third"]);
    }

    [Fact]
    public async Task A_hook_only_applies_to_its_method()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Twice.Before(_ => throw new InvalidOperationException("held")));

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
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Run.Before(_ => throw new InvalidOperationException("blocked")));

        // Act
        Func<Task> act = () => proxy.Run();

        // Assert
        var failure = await act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("blocked");
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_after_hook_that_throws_fails_the_call_after_the_inner_service_ran()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Twice.After(_ => Task.FromException(new InvalidOperationException("late"))));

        // Act
        Func<Task> act = () => proxy.Twice(1);

        // Assert
        var failure = await act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("late");
        inner.Calls.ShouldBe([nameof(CallProbe.Twice)]);
    }

    [Fact]
    public async Task An_exception_of_the_inner_service_arrives_unwrapped_and_skips_the_after_hooks()
    {
        // Arrange
        var afterHooks = 0;
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Fail.After(_ => afterHooks++));

        // Act
        Func<Task> act = () => proxy.Fail();

        // Assert
        var failure = await act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("inner failure");
        afterHooks.ShouldBe(0);
    }

    [Fact]
    public async Task A_hook_sees_the_cancellation_token_of_the_call()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        CancellationToken seen = default;
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Count.Before(call => seen = call.CancellationToken));

        // Act
        var value = await proxy.Count(3, cancellation.Token);

        // Assert
        value.ShouldBe(3);
        seen.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task A_pending_hook_can_wait_on_the_cancellation_token_of_the_call()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks.Count.Before(call => Task.Delay(Timeout.Infinite, call.CancellationToken)));

        // Act
        var pending = proxy.Count(3, cancellation.Token);
        await cancellation.CancelAsync();
        Func<Task> act = () => pending;

        // Assert
        await act.ShouldThrowAssignableTo<OperationCanceledException>();
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_method_of_an_inherited_interface_has_hooks()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Inherited.Returns(77));

        // Act
        var result = await proxy.Inherited();

        // Assert
        result.ShouldBe(77);
    }

    [Fact]
    public async Task Overloads_have_their_own_hooks_named_after_their_parameter_types()
    {
        // Arrange
        var inner = new CallProbe();
        var saved = new List<string>();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks =>
        {
            hooks.Save_NoParameters.Before(_ => saved.Add("none"));
            hooks.Save_Int32.Returns(5).Before(call => saved.Add($"value {call.Value}"));
        });

        // Act
        await proxy.Save();
        var result = await proxy.Save(1);

        // Assert
        result.ShouldBe(5);
        saved.ShouldBe(["none", "value 1"]);
        inner.Calls.ShouldBe([nameof(CallProbe.Save)]);
    }

    [Fact]
    public void A_null_inner_service_is_rejected()
    {
        // Act
        Action act = () => CallProxy.Create<ICallProbe>(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void An_interface_without_a_generated_proxy_fails_and_says_how_to_get_one()
    {
        // Arrange
        var greeter = new UnnamedGreeter();

        // Act
        Action act = () => CallProxyIndirection.Wrap<IUnnamedGreeter>(greeter);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe(
            "CallProxy has no generated proxy for IUnnamedGreeter. Name the interface in CallProxy.Create<IUnnamedGreeter> or "
            + "WrapWithCallProxy<IUnnamedGreeter>, and reference PandaBear.SharedKernel.Testing directly from the test project so its "
            + "source generator runs.");
    }

    [Fact]
    public void Hooks_configured_by_two_generated_hook_types_fail()
    {
        // Act
        Action act = () => CallProxy.Create<ICallProbe>(new CallProbe(), hooks =>
        {
            hooks.Twice.Returns(1);
            hooks.Generated<ForeignCallProbeHooks>();
        });

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message
            .ShouldBe("The hooks of ICallProbe were configured with members generated in two assemblies; configure them from one assembly.");
    }
}
