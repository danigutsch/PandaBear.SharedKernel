using Microsoft.Extensions.DependencyInjection;
using PandaBear.Results;

namespace PandaBear.Mediator.Tests;

/// <summary>Tests the registration helpers of the unique violation behavior.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class UniqueViolationRegistrationTests
{
    [Fact]
    public void Registering_the_pipeline_twice_adds_one_pipeline()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddUniqueViolationHandling<UniqueViolationTestTypes.CreateTour, Result>();
        services.AddUniqueViolationHandling<UniqueViolationTestTypes.CreateTour, Result>();

        // Assert
        services.Count(static d => d.ServiceType == typeof(IPipelineBehavior<UniqueViolationTestTypes.CreateTour, Result>)).ShouldBe(1);
    }

    [Fact]
    public async Task The_registered_pipeline_uses_the_configured_map()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddUniqueViolationHandling(static map => map.Add(UniqueViolationTestTypes.MappedRule, UniqueViolationTestTypes.MappedField, UniqueViolationTestTypes.MappedMessage))
            .AddUniqueViolationHandling<UniqueViolationTestTypes.CreateTour, Result>()
            .BuildServiceProvider();
        var behavior = provider.GetRequiredService<IPipelineBehavior<UniqueViolationTestTypes.CreateTour, Result>>();

        // Act
        var result = await behavior.Handle(
            new UniqueViolationTestTypes.CreateTour(),
            () => throw UniqueViolationTestTypes.CreateViolation(UniqueViolationTestTypes.MappedRule),
            CancellationToken.None);

        // Assert
        behavior.ShouldBeOfType<UniqueViolationBehavior<UniqueViolationTestTypes.CreateTour, Result>>();
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public async Task Without_a_registered_map_the_pipeline_answers_with_a_conflict()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddUniqueViolationHandling<UniqueViolationTestTypes.CreateTour, Result>()
            .BuildServiceProvider();
        var behavior = provider.GetRequiredService<IPipelineBehavior<UniqueViolationTestTypes.CreateTour, Result>>();

        // Act
        var result = await behavior.Handle(
            new UniqueViolationTestTypes.CreateTour(),
            () => throw UniqueViolationTestTypes.CreateViolation(UniqueViolationTestTypes.MappedRule),
            CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
    }

    [Theory]
    [InlineData("first", "email", "first taken")]
    [InlineData("second", "code", "second taken")]
    public async Task Configuring_twice_merges_the_rules_into_one_map(string rule, string field, string message)
    {
        // Arrange
        var services = new ServiceCollection()
            .AddUniqueViolationHandling(static map => map.Add("first", "email", "first taken"))
            .AddUniqueViolationHandling(static map => map.Add("second", "code", "second taken"))
            .AddUniqueViolationHandling<UniqueViolationTestTypes.CreateTour, Result>();
        await using var provider = services.BuildServiceProvider();
        var behavior = provider.GetRequiredService<IPipelineBehavior<UniqueViolationTestTypes.CreateTour, Result>>();

        // Act
        var result = await behavior.Handle(
            new UniqueViolationTestTypes.CreateTour(),
            () => throw UniqueViolationTestTypes.CreateViolation(rule),
            CancellationToken.None);

        // Assert
        services.Count(static d => d.ServiceType == typeof(UniqueRuleMap)).ShouldBe(1);
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()[field].ShouldBe([message]);
    }

    [Fact]
    public void Configuring_the_same_rule_twice_is_rejected()
    {
        // Arrange
        var services = new ServiceCollection().AddUniqueViolationHandling(static map => map.Add("r", "email", "taken"));

        // Act
        Action act = () => services.AddUniqueViolationHandling(static map => map.Add("r", "email", "taken"));

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void A_null_service_collection_is_rejected()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        Action act = () => services.AddUniqueViolationHandling(static _ => { });
        Action actPipeline = () => services.AddUniqueViolationHandling<UniqueViolationTestTypes.CreateTour, Result>();

        // Assert
        act.ShouldThrow<ArgumentNullException>();
        actPipeline.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void A_null_configure_delegate_is_rejected()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        Action act = () => services.AddUniqueViolationHandling(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
