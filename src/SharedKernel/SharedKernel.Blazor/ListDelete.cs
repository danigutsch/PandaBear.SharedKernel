using PandaBear.Results;

namespace PandaBear.Blazor;

/// <summary>
/// The delete flow of a list page: which row waits for confirmation, the error to show, and a guard that keeps a second click from
/// sending the deletion again while it runs.
/// </summary>
/// <typeparam name="TKey">The row key type.</typeparam>
/// <remarks>
/// It holds no row data and no <c>[PersistentState]</c>; the page keeps its rows and passes the callbacks that reload or edit them.
/// The guard is reset even when a callback throws. Use it on the renderer's synchronization context.
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
    public string? Error
    {
        get; private set;
    }

    /// <summary>Gets whether a deletion is running.</summary>
    public bool IsRunning
    {
        get; private set;
    }

    /// <summary>Gets whether a row waits for confirmation.</summary>
    /// <param name="key">The row key.</param>
    /// <returns><see langword="true"/> when the row waits.</returns>
    public bool IsPending(TKey key) => _confirmation.IsPending(key);

    /// <summary>Starts waiting for confirmation of a row; a row that was waiting before stops waiting.</summary>
    /// <param name="key">The row key.</param>
    public void Ask(TKey key) => _confirmation.Ask(key);

    /// <summary>Stops waiting for confirmation.</summary>
    public void Cancel() => _confirmation.Cancel();

    /// <summary>
    /// Leaves the confirmation and runs the deletion. A deletion that succeeded, or found the row already gone, calls
    /// <paramref name="removeLocal"/> so the page drops the row without reading the list again. A conflict (the row was changed or
    /// removed by someone else) calls <paramref name="reload"/>, so the list shows which one happened. Any other failure only sets
    /// <see cref="Error"/>. A call made while a deletion runs only leaves the confirmation; disable the confirm buttons with
    /// <see cref="IsRunning"/> so the click is visibly refused.
    /// </summary>
    /// <param name="key">The key of the row to delete.</param>
    /// <param name="delete">Sends the deletion; an exception propagates.</param>
    /// <param name="reload">Reads the list again.</param>
    /// <param name="removeLocal">Drops the row from the page's list.</param>
    /// <returns>A task that completes when the deletion and the follow-up have finished.</returns>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    public async Task Run(TKey key, Func<ValueTask<Result>> delete, Func<Task> reload, Action<TKey> removeLocal)
    {
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(reload);
        ArgumentNullException.ThrowIfNull(removeLocal);

        // Leave the confirmation first, so a confirm click made while another deletion runs does not leave its row waiting.
        _confirmation.Cancel();

        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        try
        {
            var result = await delete().ConfigureAwait(true);
            Error = result.IsSuccess ? null : result.Status is ResultStatus.Conflict ? _conflictMessage ?? result.ErrorDetails?.Detail ?? _failureMessage : _failureMessage;

            if (result.Status is ResultStatus.Conflict)
            {
                await reload().ConfigureAwait(true);
            }
            else if (result.IsSuccess || result.Status is ResultStatus.NotFound)
            {
                removeLocal(key);
            }
        }
        finally
        {
            IsRunning = false;
        }
    }
}
