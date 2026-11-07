using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Mediator.Tests;

/// <summary>Tests that every request is sent in its own service scope.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ScopedSenderTests
{
    [Fact]
    public async Task Each_request_is_sent_in_a_new_scope_that_is_disposed_afterwards()
    {
        // Arrange
        var probe = new ScopeProbe();
        await using var provider = new ServiceCollection()
            .AddSingleton(probe)
            .AddScoped<ISender, ProbeSender>()
            .BuildServiceProvider();
        var sender = new ScopedSender(provider.GetRequiredService<IServiceScopeFactory>(), provider);

        // Act
        var first = await sender.Send(new ProbeRequest(), CancellationToken.None);
        var second = await sender.Send(new ProbeRequest());

        // Assert
        first.ShouldNotBe(second);
        probe.DisposedScopes.ToArray().ShouldBe([first, second]);
    }

    [Fact]
    public async Task A_null_request_is_rejected()
    {
        // Arrange
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var sender = new ScopedSender(provider.GetRequiredService<IServiceScopeFactory>(), provider);

        // Act
        Func<Task> act = async () => await sender.Send<Guid>(null!, CancellationToken.None);

        // Assert
        await act.ShouldThrow<ArgumentNullException>();
    }
}
