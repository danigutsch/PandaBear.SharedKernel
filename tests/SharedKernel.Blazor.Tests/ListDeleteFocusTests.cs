using SharedKernel.Results;

namespace SharedKernel.Blazor.Tests;

/// <summary>Tests where focus goes after a list deletion and what the deletion announces.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ListDeleteFocusTests : BunitContext
{
    private const string Deleted = "Ana Souza was deleted.";

    private readonly ListDeleteProbe _page = new();
    private readonly ListDelete<Guid> _flow = new("We could not delete the customer.");
    private readonly Guid _row = Guid.NewGuid();

    /// <summary>Captures the opener of the row, the safe control and the status region, as the page's render would.</summary>
    public ListDeleteFocusTests()
    {
        _flow.Openers[_row] = this.Element("open");
        _flow.SafeControl = this.Element("keep");
        _flow.StatusRegion = this.Element("status");
    }

    [Fact]
    public async Task A_completed_deletion_is_announced_and_the_status_region_takes_focus()
    {
        // Arrange
        _flow.Ask(_row);
        await _flow.MoveFocus();

        // Act
        await _flow.Run(_row, () => ValueTask.FromResult(Result.Ok()), _page.Reload, _page.Removed.Add, Deleted);
        await _flow.MoveFocus();

        // Assert
        _flow.Status.ShouldBe(Deleted);
        this.FocusedIds().ShouldBe(["keep", "status"]);
        _flow.Openers.ContainsKey(_row).ShouldBeFalse();
    }

    [Fact]
    public async Task A_deletion_without_status_text_announces_nothing_and_moves_no_focus()
    {
        // Act
        await _flow.Run(_row, () => ValueTask.FromResult(Result.Ok()), _page.Reload, _page.Removed.Add);
        await _flow.MoveFocus();

        // Assert
        _flow.Status.ShouldBeNull();
        this.FocusedIds().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_deletion_returns_focus_to_the_opener_of_the_kept_row()
    {
        // Act
        await _flow.Run(_row, () => ValueTask.FromResult(Result.Error("boom")), _page.Reload, _page.Removed.Add, Deleted);
        await _flow.MoveFocus();

        // Assert
        _flow.Status.ShouldBeNull();
        this.FocusedIds().ShouldBe(["open"]);
    }

    [Fact]
    public async Task A_conflict_returns_focus_to_the_opener_after_the_reload()
    {
        // Act
        await _flow.Run(_row, () => ValueTask.FromResult(Result.Conflict("changed")), _page.Reload, _page.Removed.Add, Deleted);
        await _flow.MoveFocus();

        // Assert
        _page.Reloads.ShouldBe(1);
        _flow.Status.ShouldBeNull();
        this.FocusedIds().ShouldBe(["open"]);
    }

    [Fact]
    public async Task A_row_that_was_already_gone_announces_nothing_and_moves_no_focus()
    {
        // Act
        await _flow.Run(_row, () => ValueTask.FromResult(Result.NotFound("gone")), _page.Reload, _page.Removed.Add, Deleted);
        await _flow.MoveFocus();

        // Assert
        _flow.Status.ShouldBeNull();
        _flow.Error.ShouldNotBeNull();
        this.FocusedIds().ShouldBeEmpty();
        _flow.Openers.ContainsKey(_row).ShouldBeFalse();
    }

    [Fact]
    public async Task Asking_again_clears_the_previous_announcement()
    {
        // Arrange
        await _flow.Run(_row, () => ValueTask.FromResult(Result.Ok()), _page.Reload, _page.Removed.Add, Deleted);

        // Act
        _flow.Ask(Guid.NewGuid());

        // Assert
        _flow.Status.ShouldBeNull();
    }

    [Fact]
    public async Task A_deletion_that_ends_while_another_confirmation_is_open_leaves_focus_in_that_confirmation()
    {
        // Arrange
        var gate = new TaskCompletionSource<Result>();
        var running = _flow.Run(_row, () => new ValueTask<Result>(gate.Task), _page.Reload, _page.Removed.Add, Deleted);
        _flow.Ask(Guid.NewGuid());
        await _flow.MoveFocus();

        // Act
        gate.SetResult(Result.Ok());
        await running;
        await _flow.MoveFocus();

        // Assert
        _flow.Status.ShouldBe(Deleted);
        this.FocusedIds().ShouldBe(["keep"]);
    }

    [Fact]
    public async Task Cancelling_returns_focus_to_the_opener()
    {
        // Arrange
        _flow.Ask(_row);
        await _flow.MoveFocus();

        // Act
        _flow.Cancel();
        await _flow.MoveFocus();

        // Assert
        this.FocusedIds().ShouldBe(["keep", "open"]);
    }
}
