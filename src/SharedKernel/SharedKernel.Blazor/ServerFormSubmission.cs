using Microsoft.AspNetCore.Components.Forms;
using PandaBear.Results;

namespace PandaBear.Blazor;

/// <summary>
/// The state of a form that submits to a server: whether it is saving, the field errors shown on the fields and in an
/// <see cref="ErrorSummary"/>, and a fixed general error for everything that cannot be shown on a field.
/// </summary>
/// <remarks>
/// Replaces the saving flag, the binder and the error bookkeeping each form would otherwise repeat. The saving flag is reset even when
/// the save throws, so a failed call never leaves the form stuck. Dispose it with the form. Use it on the renderer's synchronization context.
/// </remarks>
public sealed class ServerFormSubmission : IDisposable
{
    private readonly EditContext _editContext;
    private readonly ServerValidationOptions _options;
    private readonly ServerValidationBinder _binder;
    private readonly Func<string, string> _fieldId;
    private readonly string _failureMessage;

    /// <summary>Initializes the submission state for an edit context.</summary>
    /// <param name="editContext">The edit context that shows the field messages.</param>
    /// <param name="options">The accepted fields and bounds.</param>
    /// <param name="fieldId">Maps a known field name to the HTML id of its control, for the summary links.</param>
    /// <param name="failureMessage">The fixed text shown when a failure cannot be shown on a field. It is never server text.</param>
    /// <exception cref="ArgumentNullException">A value is null.</exception>
    public ServerFormSubmission(EditContext editContext, ServerValidationOptions options, Func<string, string> fieldId, string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(fieldId);
        ArgumentNullException.ThrowIfNull(failureMessage);

        _binder = new ServerValidationBinder(editContext, options);
        _editContext = editContext;
        _options = options;
        _fieldId = fieldId;
        _failureMessage = failureMessage;
    }

    /// <summary>
    /// Gets the fixed text shown when the save fails with <see cref="ResultStatus.Conflict"/> (the row was changed or removed by
    /// someone else, so a retry fails the same way). It is never server text. <see langword="null"/> falls back to the failure message.
    /// </summary>
    public string? ConflictMessage
    {
        get; init;
    }

    /// <summary>
    /// Gets the fixed text shown when the save fails with <see cref="ResultStatus.NotFound"/> (the record is gone). It is never
    /// server text. <see langword="null"/> falls back to the failure message.
    /// </summary>
    public string? NotFoundMessage
    {
        get; init;
    }

    /// <summary>Gets whether a save is in progress; use it to disable the submit button.</summary>
    public bool IsSaving
    {
        get; private set;
    }

    /// <summary>Gets the general error to show, or <see langword="null"/> when there is none.</summary>
    public string? Error
    {
        get; private set;
    }

    /// <summary>Gets the error summary entries for the field errors of the last save.</summary>
    public IReadOnlyList<ErrorSummaryItem> Summary
    {
        get; private set;
    } = [];

    /// <summary>
    /// Validates the edit context and, when it has no messages, runs a save and records its outcome. A call made while a save is
    /// already running does nothing and returns <see langword="false"/>, so a double click cannot save twice. Messages already on
    /// the context for known fields (for example an input component's parse error) block the save and are listed in <see cref="Summary"/>.
    /// </summary>
    /// <param name="save">The save; a failed result is shown, an exception propagates.</param>
    /// <returns><see langword="true"/> when the save ran and succeeded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="save"/> is <see langword="null"/>.</exception>
    public async Task<bool> Submit(Func<Task<Result>> save)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (IsSaving)
        {
            return false;
        }

        Error = null;
        Summary = [];

        // Raises OnValidationRequested, which clears the server messages, so only client-side messages can remain.
        if (!_editContext.Validate())
        {
            Summary = ClientMessages();

            // Messages on fields the form does not list, or on the model, have no summary entry: show the fixed general error instead of nothing.
            Error = Summary.Count == 0 ? _failureMessage : null;
            return false;
        }

        IsSaving = true;
        Result result;
        try
        {
            result = await save().ConfigureAwait(true);
        }
        finally
        {
            IsSaving = false;
        }

        var placed = _binder.Apply(result.ValidationErrors());
        Summary = placed.ToSummaryItems(_fieldId);
        Error = ErrorFor(result.Status, result.IsSuccess, placed.HasUnplacedErrors, Summary.Count);
        return result.IsSuccess;
    }

    /// <summary>Chooses the general error for a finished save.</summary>
    /// <param name="status">The status of the save result.</param>
    /// <param name="isSuccess">Whether the save succeeded.</param>
    /// <param name="hasUnplacedErrors">Whether the server reported errors that could not be shown on a field.</param>
    /// <param name="summaryCount">The number of summary entries after the server errors were applied.</param>
    /// <returns>The fixed message to show, or <see langword="null"/> when nothing general is needed.</returns>
    internal string? ErrorFor(ResultStatus status, bool isSuccess, bool hasUnplacedErrors, int summaryCount) => status switch
    {
        ResultStatus.Invalid when !hasUnplacedErrors && summaryCount > 0 => null,
        ResultStatus.Conflict => ConflictMessage ?? _failureMessage,
        ResultStatus.NotFound => NotFoundMessage ?? _failureMessage,
        _ when isSuccess => null,
        _ => _failureMessage,
    };

    private IReadOnlyList<ErrorSummaryItem> ClientMessages() =>
        [.. _options.KnownFields
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(field => _editContext.GetValidationMessages(_editContext.Field(field))
                .Select(message => new ErrorSummaryItem(_fieldId(field), message)))];

    /// <inheritdoc />
    public void Dispose() => _binder.Dispose();
}
