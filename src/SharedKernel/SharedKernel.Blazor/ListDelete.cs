using Microsoft.AspNetCore.Components;
using SharedKernel.Results;

namespace SharedKernel.Blazor;

/// <summary>
/// The delete flow of a list page: which row waits for confirmation, the error to show, the announcement of a completed deletion,
/// where keyboard focus goes, and a guard that keeps a second click from sending the deletion again while it runs.
/// </summary>
/// <typeparam name="TKey">The row key type.</typeparam>
/// <remarks>
/// It holds no row data and no <c>[PersistentState]</c>; the page keeps its rows and passes the callbacks that reload or edit them.
/// The guard is reset even when a callback throws. Focus follows <see cref="ConfirmationState{TKey}"/>: <see cref="Ask"/> moves it to
/// <see cref="SafeControl"/> and <see cref="Cancel"/> back to the row's opener; after <see cref="Run"/> a completed deletion moves it to
/// <see cref="StatusRegion"/>, which shows <see cref="Status"/>, and a failure that keeps the row moves it to the row's opener. The page
/// captures those elements with <c>@ref</c> and calls <see cref="MoveFocus"/> from <c>OnAfterRenderAsync</c>. Use it on the renderer's
/// synchronization context.
/// </remarks>
public sealed class ListDelete<TKey>
    where TKey : notnull
{
    private readonly ConfirmationState<TKey> _confirmation = new();
    private readonly string _failureMessage;
    private readonly string? _conflictMessage;

    /// <summary>Initializes the delete flow.</summary>
    /// <param name="failureMessage">The fixed text shown when a deletion fails and retrying may not help. It is never server text.</param>
    /// <param name="conflictMessage">
    /// The fixed text shown when the deletion fails with <see cref="ResultStatus.Conflict"/>. <see langword="null"/> shows the detail
    /// of the failed result. Unlike <see cref="ServerFormSubmission.ConflictMessage"/>, which is never server text, that detail reaches the page,
    /// so it must be authored by the application and never copied from a remote service; pass text when it cannot be trusted.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="failureMessage"/> is null.</exception>
    public ListDelete(string failureMessage, string? conflictMessage = null)
    {
        ArgumentNullException.ThrowIfNull(failureMessage);

        _failureMessage = failureMessage;
        _conflictMessage = conflictMessage;
    }

    /// <summary>Gets the error to show, or <see langword="null"/> when the last deletion needs none.</summary>
    public string? Error { get; private set; }

    /// <summary>Gets whether a deletion is running.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the announcement of the last completed deletion, the <c>deletedStatus</c> passed to <see cref="Run"/>, or <see langword="null"/>.
    /// Show it in a polite status region (<c>role="status"</c>, <c>tabindex="-1"</c>) captured as <see cref="StatusRegion"/>. The next
    /// <see cref="Ask"/> or <see cref="Run"/> clears it.
    /// </summary>
    public string? Status { get; private set; }

    /// <summary>Gets the delete control of each row, which takes focus when its confirmation is cancelled or its deletion fails; see <see cref="ConfirmationState{TKey}.Openers"/>.</summary>
    public IDictionary<TKey, ElementReference> Openers => _confirmation.Openers;

    /// <summary>Gets or sets the control that takes focus when a confirmation opens, such as its Cancel button; see <see cref="ConfirmationState{TKey}.SafeControl"/>.</summary>
    public ElementReference SafeControl
    {
        get => _confirmation.SafeControl;
        set => _confirmation.SafeControl = value;
    }

    /// <summary>Gets or sets the element that shows <see cref="Status"/>, which takes focus after a completed deletion, because the row and its controls are gone.</summary>
    public ElementReference StatusRegion { get; set; }

    /// <summary>Gets whether a row waits for confirmation.</summary>
    /// <param name="key">The row key.</param>
    /// <returns><see langword="true"/> when the row waits.</returns>
    public bool IsPending(TKey key) => _confirmation.IsPending(key);

    /// <summary>
    /// Starts waiting for confirmation of a row, clears <see cref="Status"/> and asks for focus on <see cref="SafeControl"/>; a row that was
    /// waiting before stops waiting.
    /// </summary>
    /// <param name="key">The row key.</param>
    public void Ask(TKey key)
    {
        Status = null;
        _confirmation.Ask(key);
    }

    /// <summary>Stops waiting for confirmation and asks for focus on the opener of the row that was waiting.</summary>
    public void Cancel() => _confirmation.Cancel();

    /// <summary>
    /// Moves focus to the element the last <see cref="Ask"/>, <see cref="Cancel"/> or <see cref="Run"/> asked for, once; call it from
    /// <c>OnAfterRenderAsync</c>. It does nothing without a request or a captured element, and never fails the page.
    /// </summary>
    /// <returns>A task that completes when focus has moved or there was nothing to focus.</returns>
    public Task MoveFocus() => _confirmation.MoveFocus();

    /// <summary>
    /// Leaves the confirmation and runs the deletion. A deletion that succeeded, or found the row already gone, calls
    /// <paramref name="removeLocal"/> so the page drops the row without reading the list again. A conflict (the row was changed or
    /// removed by someone else) calls <paramref name="reload"/>, so the list shows which one happened. Any other failure only sets
    /// <see cref="Error"/>. A call made while a deletion runs only leaves the confirmation; disable the confirm buttons with
    /// <see cref="IsRunning"/> so the click is visibly refused. A deletion that succeeded sets <see cref="Status"/> to
    /// <paramref name="deletedStatus"/> and asks for focus on <see cref="StatusRegion"/>; a conflict or another failure asks for focus on the
    /// row's opener; a row that was already gone moves no focus, and <see cref="Error"/> reports it. When the user opened another row's
    /// confirmation while the deletion ran, focus stays in that confirmation.
    /// </summary>
    /// <param name="key">The key of the row to delete.</param>
    /// <param name="delete">Sends the deletion; an exception propagates.</param>
    /// <param name="reload">Reads the list again.</param>
    /// <param name="removeLocal">Drops the row from the page's list.</param>
    /// <param name="deletedStatus">
    /// The announcement of a completed deletion, for example "Ana Souza was deleted."; caller-authored text. <see langword="null"/>
    /// announces nothing and moves no focus.
    /// </param>
    /// <returns>A task that completes when the deletion and the follow-up have finished.</returns>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    public async Task Run(TKey key, Func<ValueTask<Result>> delete, Func<Task> reload, Action<TKey> removeLocal, string? deletedStatus = null)
    {
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(reload);
        ArgumentNullException.ThrowIfNull(removeLocal);

        // Leave the confirmation first, so a confirm click made while another deletion runs does not leave its row waiting.
        _confirmation.Close();

        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        Status = null;
        try
        {
            var result = await delete().ConfigureAwait(true);
            Error = result.IsSuccess ? null : result.Status is ResultStatus.Conflict ? _conflictMessage ?? result.ErrorDetails?.Detail ?? _failureMessage : _failureMessage;

            if (result.Status is ResultStatus.Conflict)
            {
                await reload().ConfigureAwait(true);
                FocusAfterRun(() => _confirmation.FocusOpener(key));
            }
            else if (result.IsSuccess || result.Status is ResultStatus.NotFound)
            {
                removeLocal(key);
                _confirmation.Openers.Remove(key);
                if (result.IsSuccess && deletedStatus is not null)
                {
                    Status = deletedStatus;
                    FocusAfterRun(() => _confirmation.Focus(() => StatusRegion));
                }
            }
            else
            {
                FocusAfterRun(() => _confirmation.FocusOpener(key));
            }
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void FocusAfterRun(Action request)
    {
        if (!_confirmation.HasPending)
        {
            request();
        }
    }
}
