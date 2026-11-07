using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Mediator.Tests;

/// <summary>Tests the registration helper of <see cref="ScopedSender"/>.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ScopedSenderRegistrationTests
{
    [Fact]
    public async Task The_registered_scoped_sender_resolves()
    {
        // Arrange
        await using var provider = new ServiceCollection().AddScopedSender().BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var sender = scope.ServiceProvider.GetRequiredService<ScopedSender>();

        // Assert
        sender.ShouldNotBeNull();
    }

    [Fact]
    public void The_scoped_sender_is_scoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScopedSender();

        // Assert
        services.Where(static d => d.ServiceType == typeof(ScopedSender)).ShouldHaveSingleItem().Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Registering_twice_adds_one_registration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScopedSender();
        services.AddScopedSender();

        // Assert
        services.Count(static d => d.ServiceType == typeof(ScopedSender)).ShouldBe(1);
    }

    [Fact]
    public void Registering_returns_the_same_collection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddScopedSender();

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void A_null_service_collection_is_rejected()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        Action act = () => services.AddScopedSender();

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void A_null_scope_initializer_is_rejected()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        Action act = () => services.AddScopedSender(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public async Task A_request_sent_through_the_registered_scoped_sender_works_end_to_end()
    {
        // Arrange
        var probe = new ScopeProbe();
        await using var provider = new ServiceCollection()
            .AddSingleton(probe)
            .AddScoped<ISender, ProbeSender>()
            .AddScopedSender()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ScopedSender>();

        // Act
        var first = await sender.Send(new ProbeRequest(), CancellationToken.None);
        var second = await sender.Send(new ProbeRequest());

        // Assert
        first.ShouldNotBe(second);
        probe.DisposedScopes.ToArray().ShouldBe([first, second]);
    }

    [Fact]
    public async Task The_registered_scope_initializer_runs_on_every_send()
    {
        // Arrange
        var log = new ScopeLog();
        await using var provider = new ServiceCollection()
            .AddSingleton(log)
            .AddScoped<ScopeMarker>()
            .AddScoped<ISender, MarkerSender>()
            .AddScopedSender(static (_, operation) =>
            {
                operation.GetRequiredService<ScopeMarker>().Value = "alice";
                return ValueTask.CompletedTask;
            })
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ScopedSender>();

        // Act
        var first = await sender.Send(new MarkerRequest());
        var second = await sender.Send(new MarkerRequest());

        // Assert
        first.ShouldBe("alice");
        second.ShouldBe("alice");
    }

    [Fact]
    public async Task Different_caller_scopes_get_different_senders_each_carrying_its_own_scope()
    {
        // Arrange
        var log = new ScopeLog();
        await using var provider = new ServiceCollection()
            .AddSingleton(log)
            .AddScoped<ScopeMarker>()
            .AddScoped<ISender, MarkerSender>()
            .AddScopedSender(static (caller, operation) =>
            {
                operation.GetRequiredService<ScopeMarker>().Value = caller.GetRequiredService<ScopeMarker>().Value;
                return ValueTask.CompletedTask;
            })
            .BuildServiceProvider();
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        first.ServiceProvider.GetRequiredService<ScopeMarker>().Value = "alice";
        second.ServiceProvider.GetRequiredService<ScopeMarker>().Value = "bob";

        // Act
        var firstSender = first.ServiceProvider.GetRequiredService<ScopedSender>();
        var secondSender = second.ServiceProvider.GetRequiredService<ScopedSender>();
        var firstResponse = await firstSender.Send(new MarkerRequest());
        var secondResponse = await secondSender.Send(new MarkerRequest());

        // Assert
        firstSender.ShouldNotBeSameAs(secondSender);
        first.ServiceProvider.GetRequiredService<ScopedSender>().ShouldBeSameAs(firstSender);
        firstResponse.ShouldBe("alice");
        secondResponse.ShouldBe("bob");
    }

    [Fact]
    public async Task A_second_registration_with_an_initializer_does_not_replace_the_first()
    {
        // Arrange
        var log = new ScopeLog();
        await using var provider = new ServiceCollection()
            .AddSingleton(log)
            .AddScoped<ScopeMarker>()
            .AddScoped<ISender, MarkerSender>()
            .AddScopedSender()
            .AddScopedSender(static (_, operation) =>
            {
                operation.GetRequiredService<ScopeMarker>().Value = "ignored";
                return ValueTask.CompletedTask;
            })
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var response = await scope.ServiceProvider.GetRequiredService<ScopedSender>().Send(new MarkerRequest());

        // Assert
        response.ShouldBeNull();
    }
}
