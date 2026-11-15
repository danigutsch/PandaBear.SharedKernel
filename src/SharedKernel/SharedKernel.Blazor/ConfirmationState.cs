using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor;

/// <summary>The "are you sure" state of a list in which at most one row waits for confirmation, for example before a delete.</summary>
/// <typeparam name="TKey">The row key type.</typeparam>
/// <remarks>
/// It also keeps keyboard focus on the confirmation, so it does not fall to the page when the button that was pressed disappears:
/// <see cref="Ask"/> moves focus into the confirmation (<see cref="SafeControl"/>), and <see cref="Cancel"/> returns it to the control that
/// opened it (<see cref="Openers"/>). The page captures those elements with <c>@ref</c> and calls <see cref="MoveFocus"/> from
/// <c>OnAfterRenderAsync</c>; a control it does not capture is not focused, so focus handling is opt-in. Use it on the renderer's
/// synchronization context.
/// </remarks>
public sealed class ConfirmationState<TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, ElementReference> _openers = [];
    private bool _hasPending;
    private TKey? _pending;
    private Func<ElementReference>? _focus;

    /// <summary>Gets whether a row waits for confirmation.</summary>
    public bool HasPending => _hasPending;

    /// <summary>
    /// Gets the control that opens the confirmation of each row, which takes focus again when the confirmation is cancelled. Capture it
    /// with <c>@ref="_confirmation.Openers[row.Id]"</c>; the capture of the render after <see cref="Cancel"/> is the one focused.
    /// </summary>
    public IDictionary<TKey, ElementReference> Openers => _openers;

    /// <summary>
    /// Gets or sets the control that takes focus when a confirmation opens: its first control that does no harm, such as the Cancel
    /// button or an optional reason field, never the confirming button. Capture it with <c>@ref="_confirmation.SafeControl"</c> inside the
    /// confirmation; only one confirmation is open at a time.
    /// </summary>
    public ElementReference SafeControl { get; set; }

    /// <summary>Starts waiting for confirmation of a row, and asks for focus on <see cref="SafeControl"/>; a row that was waiting before stops waiting.</summary>
    /// <param name="key">The row key.</param>
    public void Ask(TKey key)
    {
        _pending = key;
        _hasPending = true;
        _focus = () => SafeControl;
    }

    /// <summary>Gets whether a specific row waits for confirmation.</summary>
    /// <param name="key">The row key.</param>
    /// <returns><see langword="true"/> when the row waits.</returns>
    public bool IsPending(TKey key) => _hasPending && EqualityComparer<TKey>.Default.Equals(_pending, key);

    /// <summary>
    /// Stops waiting because the user declined, and asks for focus on the opener of the row that was waiting. Use <see cref="Close"/>
    /// instead after acting on the confirmation or when the page resets, where the opener may not come back.
    /// </summary>
    public void Cancel()
    {
        var hadPending = _hasPending;
        var key = _pending;
        Close();
        if (hadPending)
        {
            FocusOpener(key!);
        }
    }

    /// <summary>Stops waiting without moving focus, and drops a focus request that has not been carried out yet.</summary>
    public void Close()
    {
        _pending = default;
        _hasPending = false;
        _focus = null;
    }

    /// <summary>
    /// Moves focus to the element the last <see cref="Ask"/> or <see cref="Cancel"/> asked for, once. Call it from <c>OnAfterRenderAsync</c>,
    /// so the element captured by that render is focused. It does nothing without a request or a captured element, and never fails
    /// the page (see <see cref="FocusHelper.TryFocus"/>).
    /// </summary>
    /// <returns>A task that completes when focus has moved or there was nothing to focus.</returns>
    public Task MoveFocus()
    {
        if (_focus is not { } target)
        {
            return Task.CompletedTask;
        }

        _focus = null;
        return FocusHelper.TryFocus(target()).AsTask();
    }

    /// <summary>Asks for focus on the opener of a row at the next <see cref="MoveFocus"/>.</summary>
    /// <param name="key">The row key.</param>
    internal void FocusOpener(TKey key) => _focus = () => _openers.GetValueOrDefault(key);

    /// <summary>Asks for focus on an element at the next <see cref="MoveFocus"/>.</summary>
    /// <param name="target">Reads the element when focus moves, after the render captured it.</param>
    internal void Focus(Func<ElementReference> target) => _focus = target;
}
