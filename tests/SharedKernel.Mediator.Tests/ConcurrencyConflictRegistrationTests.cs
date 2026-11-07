using Microsoft.Extensions.DependencyInjection;
using PandaBear.Results;

namespace PandaBear.Mediator.Tests;

/// <summary>Tests the registration helper of the concurrency conflict behavior.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ConcurrencyConflictRegistrationTests
{
    [Fact]
    public void Registering_twice_adds_one_pipeline()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddConcurrencyConflictHandling<ConcurrencyConflictTestTypes.RenameTour, Result>();
        services.AddConcurrencyConflictHandling<ConcurrencyConflictTestTypes.RenameTour, Result>();

        // Assert
        services.Count(static d => d.ServiceType == typeof(IPipelineBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>)).ShouldBe(1);
    }

    [Fact]
    public async Task The_registered_pipeline_resolves_to_the_behavior()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddConcurrencyConflictHandling<ConcurrencyConflictTestTypes.RenameTour, Result>()
            .BuildServiceProvider();

        // Act
        var behavior = provider.GetRequiredService<IPipelineBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>>();

        // Assert
        behavior.ShouldBeOfType<ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>>();
    }

    [Fact]
    public void A_null_service_collection_is_rejected()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        Action act = () => services.AddConcurrencyConflictHandling<ConcurrencyConflictTestTypes.RenameTour, Result>();

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
