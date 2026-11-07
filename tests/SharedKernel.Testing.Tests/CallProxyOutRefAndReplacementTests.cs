namespace PandaBear.Testing.Tests;

/// <summary>Tests the proxy for out and ref parameters and for replacement values that do not fit the method.</summary>
public sealed class CallProxyOutRefAndReplacementTests
{
    [Fact]
    public void An_out_value_written_by_the_inner_service_reaches_the_caller()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe());

        // Act
        var found = proxy.TryGet("known", out var value);

        // Assert
        found.ShouldBeTrue();
        value.ShouldBe("found");
    }

    [Fact]
    public void A_ref_value_updated_by_the_inner_service_reaches_the_caller()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe());
        var number = 41;

        // Act
        proxy.Increment(ref number);

        // Assert
        number.ShouldBe(42);
    }

    [Fact]
    public void An_after_hook_sees_the_out_value_in_the_arguments()
    {
        // Arrange
        object? seen = null;
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.After(nameof(ICallProbe.TryGet), call =>
        {
            seen = call.Args[1];
            return Task.CompletedTask;
        }));

        // Act
        proxy.TryGet("known", out _);

        // Assert
        seen.ShouldBe("found");
    }

    [Fact]
    public async Task A_null_replacement_for_a_task_of_a_value_type_fails_naming_the_method()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Returns(nameof(ICallProbe.Twice), null));

        // Act
        Func<Task> act = async () => await proxy.Twice(1);

        // Assert
        var failure = await act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("CallProxy.Returns for 'Twice' cannot supply null where System.Int32 is expected.");
    }

    [Fact]
    public async Task A_null_replacement_for_a_value_task_of_a_value_type_fails_naming_the_method()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Returns(nameof(ICallProbe.Thrice), null));

        // Act
        Func<Task> act = async () => await proxy.Thrice(1);

        // Assert
        var failure = await act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("CallProxy.Returns for 'Thrice' cannot supply null where System.Int32 is expected.");
    }

    [Fact]
    public void A_null_replacement_for_a_synchronous_value_type_fails_naming_the_method()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Returns(nameof(ICallProbe.Add), null));

        // Act
        Action act = () => proxy.Add(1, 2);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message
            .ShouldBe("CallProxy.Returns for 'Add' cannot supply null where System.Int32 is expected.");
    }

    [Fact]
    public async Task A_replacement_of_the_wrong_type_fails_without_exposing_the_value()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Returns(nameof(ICallProbe.Twice), "secret"));

        // Act
        Func<Task> act = async () => await proxy.Twice(1);

        // Assert
        var failure = await act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("CallProxy.Returns for 'Twice' cannot supply System.String where System.Int32 is expected.");
    }

    [Fact]
    public void A_replacement_of_the_wrong_type_for_a_synchronous_method_fails_the_same_way()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Returns(nameof(ICallProbe.Add), "secret"));

        // Act
        Action act = () => proxy.Add(1, 2);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message
            .ShouldBe("CallProxy.Returns for 'Add' cannot supply System.String where System.Int32 is expected.");
    }

    [Fact]
    public async Task A_null_replacement_for_a_reference_type_result_is_allowed()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Returns(nameof(ICallProbe.FindName), null));

        // Act
        var name = await proxy.FindName();

        // Assert
        name.ShouldBeNull();
    }

    [Fact]
    public async Task A_null_replacement_for_a_nullable_value_type_result_is_allowed()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Returns(nameof(ICallProbe.FindNumber), null));

        // Act
        var number = await proxy.FindNumber();

        // Assert
        number.ShouldBeNull();
    }
}
