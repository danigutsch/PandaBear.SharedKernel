namespace PandaBear.Testing.Tests;

/// <summary>Tests that a hook registered for a name or method kind that cannot work fails when the proxy is created.</summary>
public sealed class CallProxyValidationTests
{
    [Fact]
    public void An_unknown_name_in_before_throws_and_names_it()
    {
        // Arrange
        var inner = new CallProbe();

        // Act
        Action act = () => CallProxy.Create<ICallProbe>(inner, hooks => hooks.Before("Twicee", _ => Task.CompletedTask));

        // Assert
        var failure = act.ShouldThrow<ArgumentException>();
        failure.Message.ShouldContain("Twicee", StringComparison.Ordinal);
        failure.Message.ShouldContain("Twice", StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_name_in_after_throws_and_names_it()
    {
        // Arrange
        var inner = new CallProbe();

        // Act
        Action act = () => CallProxy.Create<ICallProbe>(inner, hooks => hooks.After("Nope", _ => Task.CompletedTask));

        // Assert
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("Nope", StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_name_in_returns_throws_and_names_it()
    {
        // Arrange
        var inner = new CallProbe();

        // Act
        Action act = () => CallProxy.Create<ICallProbe>(inner, hooks => hooks.Returns("Missing", 1));

        // Assert
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("Missing", StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_on_a_task_method_throws_naming_the_method()
    {
        // Arrange
        var inner = new CallProbe();

        // Act
        Action act = () => CallProxy.Create<ICallProbe>(inner, hooks => hooks.Returns(nameof(ICallProbe.Run), null));

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("Returns cannot be used for 'Run', which returns no value.");
    }

    [Fact]
    public void Returns_on_a_value_task_method_throws_naming_the_method()
    {
        // Arrange
        var inner = new CallProbe();

        // Act
        Action act = () => CallProxy.Create<ICallProbe>(inner, hooks => hooks.Returns(nameof(ICallProbe.Pause), null));

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("Returns cannot be used for 'Pause', which returns no value.");
    }

    [Fact]
    public void Returns_on_a_void_method_throws_naming_the_method()
    {
        // Arrange
        var inner = new CallProbe();

        // Act
        Action act = () => CallProxy.Create<ICallProbe>(inner, hooks => hooks.Returns(nameof(ICallProbe.Touch), null));

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("Returns cannot be used for 'Touch', which returns no value.");
    }

    [Fact]
    public void Returns_on_a_method_with_an_out_parameter_throws()
    {
        // Arrange
        var inner = new CallProbe();

        // Act
        Action act = () => CallProxy.Create<ICallProbe>(inner, hooks => hooks.Returns(nameof(ICallProbe.TryGet), true));

        // Assert
        var failure = act.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldContain("'TryGet'", StringComparison.Ordinal);
        failure.Message.ShouldContain("out/ref", StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_on_a_method_with_a_ref_parameter_throws()
    {
        // Arrange
        var inner = new CallProbe();

        // Act
        Action act = () => CallProxy.Create<ICallProbe>(inner, hooks => hooks.Returns(nameof(ICallProbe.Increment), null));

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("'Increment'", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Valid_registrations_for_each_kind_of_method_work()
    {
        // Arrange
        var inner = new CallProbe();
        var proxy = CallProxy.Create<ICallProbe>(inner, hooks => hooks
            .Returns(nameof(ICallProbe.Twice), 1)
            .Returns(nameof(ICallProbe.Thrice), 2)
            .Returns(nameof(ICallProbe.Add), 3)
            .Before(nameof(ICallProbe.Run), _ => Task.CompletedTask)
            .After(nameof(ICallProbe.Touch), _ => Task.CompletedTask)
            .After(nameof(ICallProbe.TryGet), _ => Task.CompletedTask));

        // Act
        var twice = await proxy.Twice(5);
        var thrice = await proxy.Thrice(5);
        var sum = proxy.Add(1, 1);

        // Assert
        new[] { twice, thrice, sum }.ShouldBe([1, 2, 3]);
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_name_from_an_inherited_interface_is_accepted()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Returns(nameof(ICallProbeBase.Inherited), 77));

        // Act
        var result = await proxy.Inherited();

        // Assert
        result.ShouldBe(77);
    }

    [Fact]
    public async Task An_overload_that_returns_a_value_makes_returns_valid_for_the_name()
    {
        // Arrange
        var proxy = CallProxy.Create<ICallProbe>(new CallProbe(), hooks => hooks.Returns(nameof(ICallProbe.Save), 5));

        // Act
        var result = await proxy.Save(1);

        // Assert
        result.ShouldBe(5);
    }
}
