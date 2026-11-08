using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Mediator.Tests;

/// <summary>Tests the order semantics of the <see cref="ScopedSender"/> registration helpers.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ScopedSenderRegistrationOrderTests
{
    [Fact]
    public async Task An_initializer_registered_after_the_parameterless_overload_runs()
    {
        // Arrange
        await using var provider = ScopedSenderOrderProviders.Build(static services => services
            .AddScopedSender()
            .AddScopedSender(static (_, operation) => ScopedSenderOrderProviders.Mark(operation, "alice")));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var response = await scope.ServiceProvider.GetRequiredService<ScopedSender>().Send(new MarkerRequest());

        // Assert
        response.ShouldBe("alice");
    }

    [Fact]
    public async Task The_parameterless_overload_registered_after_an_initializer_keeps_it()
    {
        // Arrange
        await using var provider = ScopedSenderOrderProviders.Build(static services => services
            .AddScopedSender(static (_, operation) => ScopedSenderOrderProviders.Mark(operation, "alice"))
            .AddScopedSender());
        await using var scope = provider.CreateAsyncScope();

        // Act
        var response = await scope.ServiceProvider.GetRequiredService<ScopedSender>().Send(new MarkerRequest());

        // Assert
        response.ShouldBe("alice");
    }

    [Fact]
    public async Task The_last_initializer_registered_wins()
    {
        // Arrange
        await using var provider = ScopedSenderOrderProviders.Build(static services => services
            .AddScopedSender(static (_, operation) => ScopedSenderOrderProviders.Mark(operation, "first"))
            .AddScopedSender(static (_, operation) => ScopedSenderOrderProviders.Mark(operation, "second")));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var response = await scope.ServiceProvider.GetRequiredService<ScopedSender>().Send(new MarkerRequest());

        // Assert
        response.ShouldBe("second");
    }

    [Fact]
    public void Replacing_with_an_initializer_leaves_one_scoped_registration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScopedSender();
        services.AddScopedSender(static (_, _) => ValueTask.CompletedTask);

        // Assert
        services.Where(static d => d.ServiceType == typeof(ScopedSender)).ShouldHaveSingleItem().Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }
}
