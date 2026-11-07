using PandaBear.Results;
using PandaBear.Testing.Mediator;

namespace PandaBear.Mediator.Tests;

/// <summary>Tests that the unique violation behavior turns only a unique violation into a failed result.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class UniqueViolationBehaviorTests
{
    private const string ExpectedConflict = "A value that must be unique is already in use.";

    [Fact]
    public async Task A_mapped_rule_becomes_an_invalid_result_with_the_field_error()
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.CreateTour, Result>(UniqueViolationTestTypes.CreateMap());

        // Act
        var result = await behavior.Handle(
            new UniqueViolationTestTypes.CreateTour(),
            () => throw UniqueViolationTestTypes.CreateViolation(UniqueViolationTestTypes.MappedRule),
            CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.Count.ShouldBe(1);
        errors[UniqueViolationTestTypes.MappedField].ShouldBe([UniqueViolationTestTypes.MappedMessage]);
    }

    [Fact]
    public async Task A_mapped_rule_becomes_an_invalid_result_with_a_value_type()
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.LoadTour, Result<string>>(UniqueViolationTestTypes.CreateMap());

        // Act
        var result = await behavior.Handle(
            new UniqueViolationTestTypes.LoadTour(),
            () => throw UniqueViolationTestTypes.CreateViolation(UniqueViolationTestTypes.MappedRule),
            CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors[UniqueViolationTestTypes.MappedField].ShouldBe([UniqueViolationTestTypes.MappedMessage]);
    }

    [Fact]
    public async Task An_unmapped_rule_becomes_a_conflict_result()
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.CreateTour, Result>(UniqueViolationTestTypes.CreateMap());

        // Act
        var result = await behavior.Handle(
            new UniqueViolationTestTypes.CreateTour(),
            () => throw UniqueViolationTestTypes.CreateViolation(UniqueViolationTestTypes.UnmappedRule),
            CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe(ExpectedConflict);
    }

    [Fact]
    public async Task An_unmapped_rule_becomes_a_conflict_result_with_a_value_type()
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.LoadTour, Result<string>>(UniqueViolationTestTypes.CreateMap());

        // Act
        var result = await behavior.Handle(
            new UniqueViolationTestTypes.LoadTour(),
            () => throw UniqueViolationTestTypes.CreateViolation(UniqueViolationTestTypes.UnmappedRule),
            CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe(ExpectedConflict);
    }

    [Fact]
    public async Task Without_a_map_every_rule_becomes_a_conflict_result()
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.CreateTour, Result>();

        // Act
        var result = await behavior.Handle(
            new UniqueViolationTestTypes.CreateTour(),
            () => throw UniqueViolationTestTypes.CreateViolation(UniqueViolationTestTypes.MappedRule),
            CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe(ExpectedConflict);
    }

    [Theory]
    [InlineData(UniqueViolationTestTypes.MappedRule)]
    [InlineData(UniqueViolationTestTypes.UnmappedRule)]
    public async Task Neither_the_rule_name_nor_the_exception_text_reaches_the_result(string rule)
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.CreateTour, Result>(UniqueViolationTestTypes.CreateMap());

        // Act
        var result = await behavior.Handle(
            new UniqueViolationTestTypes.CreateTour(),
            () => throw UniqueViolationTestTypes.CreateViolation(rule),
            CancellationToken.None);

        // Assert
        var text = result.ErrorDetails.ShouldNotBeNull().ToString() + string.Join('|', result.ErrorDetails.ValidationErrors?.SelectMany(static p => p.Value.Prepend(p.Key)) ?? []);
        text.Contains(UniqueViolationTestTypes.MappedRule, StringComparison.Ordinal).ShouldBeFalse();
        text.Contains(UniqueViolationTestTypes.UnmappedRule, StringComparison.Ordinal).ShouldBeFalse();
        text.Contains(UniqueViolationTestTypes.Secret, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public async Task A_successful_result_passes_through_untouched()
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.LoadTour, Result<string>>(UniqueViolationTestTypes.CreateMap());
        var expected = Result.Ok("vt-42");

        // Act
        var result = await behavior.Handle(new UniqueViolationTestTypes.LoadTour(), () => ValueTask.FromResult(expected), CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
        result.Value.ShouldBe("vt-42");
    }

    [Fact]
    public async Task A_failed_result_passes_through_untouched()
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.CreateTour, Result>(UniqueViolationTestTypes.CreateMap());
        var expected = Result.NotFound("missing");

        // Act
        var result = await behavior.Handle(new UniqueViolationTestTypes.CreateTour(), () => ValueTask.FromResult(expected), CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Another_exception_propagates_unchanged()
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.CreateTour, Result>(UniqueViolationTestTypes.CreateMap());
        var failure = new InvalidOperationException("boom");

        // Act
        Func<Task> act = async () =>
            await behavior.Handle(new UniqueViolationTestTypes.CreateTour(), () => throw failure, CancellationToken.None);
        var thrown = await act.ShouldThrow<InvalidOperationException>();

        // Assert
        thrown.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task Cancellation_propagates_unchanged()
    {
        // Arrange
        var behavior = new UniqueViolationBehavior<UniqueViolationTestTypes.CreateTour, Result>(UniqueViolationTestTypes.CreateMap());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var cancellation = new OperationCanceledException(cts.Token);

        // Act
        Func<Task> act = async () =>
            await behavior.Handle(new UniqueViolationTestTypes.CreateTour(), () => throw cancellation, cts.Token);
        var thrown = await act.ShouldThrow<OperationCanceledException>();

        // Assert
        thrown.ShouldBeSameAs(cancellation);
    }

    [Fact]
    public async Task Dispatch_through_the_reference_mediator_converts_a_violation_thrown_by_the_handler()
    {
        // Arrange
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(new UniqueViolationTestTypes.ThrowingCreateHandler(UniqueViolationTestTypes.CreateViolation(UniqueViolationTestTypes.MappedRule)))
            .AddPipeline(new UniqueViolationBehavior<UniqueViolationTestTypes.CreateTour, Result>(UniqueViolationTestTypes.CreateMap()))
            .Build();

        // Act
        var result = await dispatcher.Send(new UniqueViolationTestTypes.CreateTour(), CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()[UniqueViolationTestTypes.MappedField]
            .ShouldBe([UniqueViolationTestTypes.MappedMessage]);
    }
}
