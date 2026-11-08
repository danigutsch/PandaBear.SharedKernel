using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Mediator.Tests;

/// <summary>Tests the optional scope initializer of <see cref="ScopedSender"/>.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ScopedSenderInitializerTests
{
    [Fact]
    public async Task The_initializer_runs_once_per_send_on_the_new_scope_before_the_sender_resolves()
    {
        // Arrange
        var log = new ScopeLog();
        await using var provider = ScopeMarkerProviders.Create(log);
        var sender = new ScopedSender(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider,
            (_, operation) =>
            {
                log.Entries.Add("initializer");
                operation.GetRequiredService<ScopeMarker>().Value = "alice";
                return ValueTask.CompletedTask;
            });

        // Act
        var first = await sender.Send(new MarkerRequest());
        var second = await sender.Send(new MarkerRequest());

        // Assert
        first.ShouldBe("alice");
        second.ShouldBe("alice");
        log.Entries.ToArray().ShouldBe([
            "initializer", "marker created", "sender created", "marker disposed",
            "initializer", "marker created", "sender created", "marker disposed",
        ]);
    }

    [Fact]
    public async Task Without_an_initializer_the_scope_state_is_not_initialized()
    {
        // Arrange
        var log = new ScopeLog();
        await using var provider = ScopeMarkerProviders.Create(log);
        var sender = new ScopedSender(provider.GetRequiredService<IServiceScopeFactory>(), provider);

        // Act
        var response = await sender.Send(new MarkerRequest());

        // Assert
        response.ShouldBeNull();
    }

    [Fact]
    public async Task A_failing_initializer_propagates_and_the_scope_is_still_disposed()
    {
        // Arrange
        var log = new ScopeLog();
        await using var provider = ScopeMarkerProviders.Create(log);
        var sender = new ScopedSender(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider,
            (_, operation) =>
            {
                operation.GetRequiredService<ScopeMarker>();
                throw new InvalidOperationException("no identity");
            });

        // Act
        Func<Task> act = async () => await sender.Send(new MarkerRequest());

        // Assert
        (await act.ShouldThrow<InvalidOperationException>()).Message.ShouldBe("no identity");
        log.Entries.ToArray().ShouldBe(["marker created", "marker disposed"]);
    }

    [Fact]
    public async Task The_initializer_receives_the_caller_scope_first_and_the_operation_scope_second()
    {
        // Arrange
        var log = new ScopeLog();
        await using var provider = ScopeMarkerProviders.Create(log);
        await using var callerScope = provider.CreateAsyncScope();
        callerScope.ServiceProvider.GetRequiredService<ScopeMarker>().Value = "caller";
        var recorder = new InitializerRecorder();
        var sender = new ScopedSender(
            provider.GetRequiredService<IServiceScopeFactory>(),
            callerScope.ServiceProvider,
            recorder.Record);

        // Act
        await sender.Send(new MarkerRequest());

        // Assert
        recorder.Caller.ShouldBeSameAs(callerScope.ServiceProvider);
        recorder.CallerMarker!.Value.ShouldBe("caller");
        recorder.OperationMarker.ShouldNotBeSameAs(recorder.CallerMarker);
        recorder.OperationMarker!.Value.ShouldBeNull();
        recorder.Operation.ShouldNotBeSameAs(callerScope.ServiceProvider);
    }

    [Fact]
    public async Task The_second_provider_is_the_scope_the_handler_sender_comes_from()
    {
        // Arrange
        var log = new ScopeLog();
        await using var provider = ScopeMarkerProviders.Create(log);
        await using var callerScope = provider.CreateAsyncScope();
        var recorder = new InitializerRecorder();
        var sender = new ScopedSender(
            provider.GetRequiredService<IServiceScopeFactory>(),
            callerScope.ServiceProvider,
            (caller, operation) =>
            {
                operation.GetRequiredService<ScopeMarker>().Value = "operation";
                return recorder.Record(caller, operation);
            });

        // Act
        var response = await sender.Send(new MarkerRequest());

        // Assert
        response.ShouldBe("operation");
        recorder.OperationMarker!.Value.ShouldBe("operation");
    }

    [Fact]
    public async Task A_value_copied_from_the_caller_scope_is_visible_to_the_handler()
    {
        // Arrange
        var log = new ScopeLog();
        await using var provider = ScopeMarkerProviders.Create(log);
        await using var circuitScope = provider.CreateAsyncScope();
        circuitScope.ServiceProvider.GetRequiredService<ScopeMarker>().Value = "alice";
        var sender = new ScopedSender(
            provider.GetRequiredService<IServiceScopeFactory>(),
            circuitScope.ServiceProvider,
            (caller, operation) =>
            {
                operation.GetRequiredService<ScopeMarker>().Value = caller.GetRequiredService<ScopeMarker>().Value;
                return ValueTask.CompletedTask;
            });

        // Act
        var response = await sender.Send(new MarkerRequest());

        // Assert
        response.ShouldBe("alice");
    }
}
