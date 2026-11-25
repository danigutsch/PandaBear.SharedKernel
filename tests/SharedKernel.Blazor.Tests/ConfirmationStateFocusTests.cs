namespace SharedKernel.Blazor.Tests;

/// <summary>Tests where focus goes when an inline confirmation opens, is cancelled or is closed.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ConfirmationStateFocusTests : BunitContext
{
    private readonly ConfirmationState<int> _state = new();

    [Fact]
    public async Task Asking_moves_focus_to_the_safe_control_after_the_render()
    {
        // Arrange
        _state.SafeControl = this.Element("keep");

        // Act
        _state.Ask(1);
        var beforeRender = this.FocusedIds();
        await _state.MoveFocus();

        // Assert
        beforeRender.ShouldBeEmpty();
        this.FocusedIds().ShouldBe(["keep"]);
    }

    [Fact]
    public async Task Cancelling_returns_focus_to_the_opener_of_the_waiting_key()
    {
        // Arrange
        _state.Openers[1] = this.Element("open-1");
        _state.Openers[2] = this.Element("open-2");
        _state.Ask(2);
        await _state.MoveFocus();

        // Act
        _state.Cancel();
        await _state.MoveFocus();

        // Assert
        _state.HasPending.ShouldBeFalse();
        this.FocusedIds().ShouldBe(["open-2"]);
    }

    [Fact]
    public async Task The_opener_captured_by_the_render_after_cancelling_takes_focus()
    {
        // Arrange
        _state.Openers[1] = this.Element("open-1-before");
        _state.Ask(1);
        _state.Cancel();

        // Act
        _state.Openers[1] = this.Element("open-1-after");
        await _state.MoveFocus();

        // Assert
        this.FocusedIds().ShouldBe(["open-1-after"]);
    }

    [Fact]
    public async Task Closing_moves_no_focus_and_drops_a_pending_request()
    {
        // Arrange
        _state.SafeControl = this.Element("keep");
        _state.Openers[1] = this.Element("open-1");
        _state.Ask(1);

        // Act
        _state.Close();
        await _state.MoveFocus();

        // Assert
        _state.HasPending.ShouldBeFalse();
        this.FocusedIds().ShouldBeEmpty();
    }

    [Fact]
    public async Task Focus_moves_once_per_request()
    {
        // Arrange
        _state.SafeControl = this.Element("keep");
        _state.Ask(1);

        // Act
        await _state.MoveFocus();
        await _state.MoveFocus();

        // Assert
        this.FocusedIds().ShouldBe(["keep"]);
    }

    [Fact]
    public async Task Controls_the_page_did_not_capture_are_not_focused()
    {
        // Act
        _state.Ask(1);
        await _state.MoveFocus();
        _state.Cancel();
        await _state.MoveFocus();

        // Assert
        this.FocusedIds().ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancelling_without_a_waiting_key_moves_no_focus()
    {
        // Arrange
        _state.Openers[1] = this.Element("open-1");

        // Act
        _state.Cancel();
        await _state.MoveFocus();

        // Assert
        this.FocusedIds().ShouldBeEmpty();
    }
}
