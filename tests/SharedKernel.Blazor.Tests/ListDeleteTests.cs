using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the delete flow of a list page.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ListDeleteTests
{
    private const string FailureMessage = "We could not delete the customer. It may already be gone.";

    private readonly ListDeleteProbe _page = new();

    [Fact]
    public async Task A_successful_deletion_removes_the_row_locally_without_reloading()
    {
        // Arrange
        var id = Guid.NewGuid();
        var flow = new ListDelete<Guid>(FailureMessage);
        flow.Ask(id);

        // Act
        await flow.Run(id, () => ValueTask.FromResult(Result.Ok()), _page.Reload, _page.Removed.Add);

        // Assert
        _page.Removed.ShouldBe([id]);
        _page.Reloads.ShouldBe(0);
        flow.Error.ShouldBeNull();
        flow.IsPending(id).ShouldBeFalse();
        flow.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task A_missing_row_is_removed_locally_and_the_failure_message_is_shown()
    {
        // Arrange
        var id = Guid.NewGuid();
        var flow = new ListDelete<Guid>(FailureMessage);

        // Act
        await flow.Run(id, () => ValueTask.FromResult(Result.NotFound("gone")), _page.Reload, _page.Removed.Add);

        // Assert
        _page.Removed.ShouldBe([id]);
        _page.Reloads.ShouldBe(0);
        flow.Error.ShouldBe(FailureMessage);
    }

    [Fact]
    public async Task A_conflict_shows_the_result_detail_and_reloads_the_list()
    {
        // Arrange
        var flow = new ListDelete<Guid>(FailureMessage);

        // Act
        await flow.Run(Guid.NewGuid(), () => ValueTask.FromResult(Result.Conflict("Someone changed it.")), _page.Reload, _page.Removed.Add);

        // Assert
        flow.Error.ShouldBe("Someone changed it.");
        _page.Reloads.ShouldBe(1);
        _page.Removed.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_conflict_message_given_to_the_flow_replaces_the_result_detail()
    {
        // Arrange
        var flow = new ListDelete<Guid>(FailureMessage, "Reload and try again.");

        // Act
        await flow.Run(Guid.NewGuid(), () => ValueTask.FromResult(Result.Conflict("Someone changed it.")), _page.Reload, _page.Removed.Add);

        // Assert
        flow.Error.ShouldBe("Reload and try again.");
    }

    [Fact]
    public async Task Another_failure_shows_the_failure_message_and_keeps_the_row()
    {
        // Arrange
        var flow = new ListDelete<Guid>(FailureMessage);

        // Act
        await flow.Run(Guid.NewGuid(), () => ValueTask.FromResult(Result.Error("boom")), _page.Reload, _page.Removed.Add);

        // Assert
        flow.Error.ShouldBe(FailureMessage);
        _page.Reloads.ShouldBe(0);
        _page.Removed.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_later_success_clears_the_previous_error()
    {
        // Arrange
        var flow = new ListDelete<Guid>(FailureMessage);
        await flow.Run(Guid.NewGuid(), () => ValueTask.FromResult(Result.Error("boom")), _page.Reload, _page.Removed.Add);

        // Act
        await flow.Run(Guid.NewGuid(), () => ValueTask.FromResult(Result.Ok()), _page.Reload, _page.Removed.Add);

        // Assert
        flow.Error.ShouldBeNull();
    }

    [Fact]
    public async Task A_second_call_while_the_deletion_runs_does_nothing()
    {
        // Arrange
        var flow = new ListDelete<Guid>(FailureMessage);
        var gate = new TaskCompletionSource<Result>();
        var sends = 0;
        var first = flow.Run(Guid.NewGuid(), () =>
        {
            sends++;
            return new ValueTask<Result>(gate.Task);
        }, _page.Reload, _page.Removed.Add);

        // Act
        var runningDuringFirst = flow.IsRunning;
        await flow.Run(Guid.NewGuid(), () =>
        {
            sends++;
            return ValueTask.FromResult(Result.Ok());
        }, _page.Reload, _page.Removed.Add);
        gate.SetResult(Result.Ok());
        await first;

        // Assert
        runningDuringFirst.ShouldBeTrue();
        sends.ShouldBe(1);
        _page.Removed.Count.ShouldBe(1);
        flow.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task An_exception_in_the_deletion_resets_the_guard_and_leaves_the_confirmation()
    {
        // Arrange
        var id = Guid.NewGuid();
        var flow = new ListDelete<Guid>(FailureMessage);
        flow.Ask(id);

        // Act
        Func<Task> act = () => flow.Run(id, () => throw new InvalidOperationException("down"), _page.Reload, _page.Removed.Add);
        var thrown = await act.ShouldThrow<InvalidOperationException>();

        // Assert
        thrown.Message.ShouldBe("down");
        flow.IsRunning.ShouldBeFalse();
        flow.IsPending(id).ShouldBeFalse();
    }

    [Fact]
    public async Task An_exception_in_the_reload_resets_the_guard()
    {
        // Arrange
        var flow = new ListDelete<Guid>(FailureMessage);

        // Act
        Func<Task> act = () => flow.Run(Guid.NewGuid(), () => ValueTask.FromResult(Result.Conflict("changed")), () => throw new InvalidOperationException("down"), _page.Removed.Add);
        await act.ShouldThrow<InvalidOperationException>();
        await flow.Run(Guid.NewGuid(), () => ValueTask.FromResult(Result.Ok()), _page.Reload, _page.Removed.Add);

        // Assert
        flow.IsRunning.ShouldBeFalse();
        _page.Removed.Count.ShouldBe(1);
    }

    [Fact]
    public void Asking_and_cancelling_drive_the_confirmation()
    {
        // Arrange
        var id = Guid.NewGuid();
        var flow = new ListDelete<Guid>(FailureMessage);

        // Act
        flow.Ask(id);
        var pending = flow.IsPending(id);
        flow.Cancel();

        // Assert
        pending.ShouldBeTrue();
        flow.IsPending(id).ShouldBeFalse();
    }

    [Fact]
    public async Task A_confirm_click_on_another_row_while_a_deletion_runs_leaves_its_confirmation()
    {
        // Arrange
        var rowA = Guid.NewGuid();
        var rowB = Guid.NewGuid();
        var flow = new ListDelete<Guid>(FailureMessage);
        var gate = new TaskCompletionSource<Result>();
        flow.Ask(rowA);
        var first = flow.Run(rowA, () => new ValueTask<Result>(gate.Task), _page.Reload, _page.Removed.Add);
        flow.Ask(rowB);

        // Act
        await flow.Run(rowB, () => ValueTask.FromResult(Result.Ok()), _page.Reload, _page.Removed.Add);
        gate.SetResult(Result.Ok());
        await first;

        // Assert
        flow.IsPending(rowB).ShouldBeFalse();
        _page.Removed.ShouldBe([rowA]);
    }

    [Fact]
    public void A_value_task_returning_send_is_accepted_as_the_deletion()
    {
        // Arrange
        var flow = new ListDelete<Guid>(FailureMessage);
        // Act
        var running = flow.Run(Guid.NewGuid(), () => ListDeleteProbe.Send(Guid.Empty), _page.Reload, _page.Removed.Add);

        // Assert
        running.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Missing_callbacks_and_messages_are_rejected()
    {
        // Arrange
        var flow = new ListDelete<Guid>(FailureMessage);

        // Act
        Action constructing = () => _ = new ListDelete<Guid>(null!);
        Func<Task> noDelete = () => flow.Run(Guid.NewGuid(), null!, _page.Reload, _page.Removed.Add);
        Func<Task> noReload = () => flow.Run(Guid.NewGuid(), () => ValueTask.FromResult(Result.Ok()), null!, _page.Removed.Add);
        Func<Task> noRemove = () => flow.Run(Guid.NewGuid(), () => ValueTask.FromResult(Result.Ok()), _page.Reload, null!);

        // Assert
        constructing.ShouldThrow<ArgumentNullException>();
        await noDelete.ShouldThrow<ArgumentNullException>();
        await noReload.ShouldThrow<ArgumentNullException>();
        await noRemove.ShouldThrow<ArgumentNullException>();
    }
}
