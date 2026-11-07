using PandaBear.Results;
using PandaBear.Testing.Mediator;

namespace PandaBear.Mediator.Tests;

/// <summary>Tests that the concurrency conflict behavior turns only a concurrency conflict into a failed result.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ConcurrencyConflictBehaviorTests
{
    private const string ExpectedMessage = "The data was changed or removed by someone else. Reload it and try again.";

    [Fact]
    public async Task A_conflict_becomes_a_conflict_result()
    {
        // Arrange
        var behavior = new ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>();

        // Act
        var result = await behavior.Handle(
            new ConcurrencyConflictTestTypes.RenameTour(),
            () => throw ConcurrencyConflictTestTypes.CreateConflict(),
            CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task A_conflict_becomes_a_conflict_result_with_a_value_type()
    {
        // Arrange
        var behavior = new ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.LoadTour, Result<string>>();

        // Act
        var result = await behavior.Handle(
            new ConcurrencyConflictTestTypes.LoadTour(),
            () => throw ConcurrencyConflictTestTypes.CreateConflict(),
            CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task The_exception_text_never_reaches_the_result()
    {
        // Arrange
        var behavior = new ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>();

        // Act
        var result = await behavior.Handle(
            new ConcurrencyConflictTestTypes.RenameTour(),
            () => throw ConcurrencyConflictTestTypes.CreateConflict(),
            CancellationToken.None);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ToString().Contains(ConcurrencyConflictTestTypes.Secret, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public async Task A_successful_result_passes_through_untouched()
    {
        // Arrange
        var behavior = new ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.LoadTour, Result<string>>();
        var expected = Result.Ok("vt-42");

        // Act
        var result = await behavior.Handle(new ConcurrencyConflictTestTypes.LoadTour(), () => ValueTask.FromResult(expected), CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
        result.Value.ShouldBe("vt-42");
    }

    [Fact]
    public async Task A_failed_result_passes_through_untouched()
    {
        // Arrange
        var behavior = new ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>();
        var expected = Result.NotFound("missing");

        // Act
        var result = await behavior.Handle(new ConcurrencyConflictTestTypes.RenameTour(), () => ValueTask.FromResult(expected), CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Another_exception_propagates_unchanged()
    {
        // Arrange
        var behavior = new ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>();
        var failure = new InvalidOperationException("boom");

        // Act
        Func<Task> act = async () =>
            await behavior.Handle(new ConcurrencyConflictTestTypes.RenameTour(), () => throw failure, CancellationToken.None);
        var thrown = await act.ShouldThrow<InvalidOperationException>();

        // Assert
        thrown.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task Cancellation_propagates_unchanged()
    {
        // Arrange
        var behavior = new ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var cancellation = new OperationCanceledException(cts.Token);

        // Act
        Func<Task> act = async () =>
            await behavior.Handle(new ConcurrencyConflictTestTypes.RenameTour(), () => throw cancellation, cts.Token);
        var thrown = await act.ShouldThrow<OperationCanceledException>();

        // Assert
        thrown.ShouldBeSameAs(cancellation);
    }

    [Fact]
    public async Task Dispatch_through_the_reference_mediator_converts_a_conflict_thrown_by_the_handler()
    {
        // Arrange
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(new ConcurrencyConflictTestTypes.ThrowingRenameHandler(ConcurrencyConflictTestTypes.CreateConflict()))
            .AddPipeline(new ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>())
            .Build();

        // Act
        var result = await dispatcher.Send(new ConcurrencyConflictTestTypes.RenameTour(), CancellationToken.None);

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe(ExpectedMessage);
    }
}
