using Microsoft.AspNetCore.Components.Forms;
using PandaBear.Results;

namespace PandaBear.Blazor;

/// <summary>
/// The lifecycle of a page that creates a record (no key) or edits one (a key): follows the route key, loads the record, fills the form,
/// and builds the <see cref="EditContext"/> and the <see cref="ServerFormSubmission"/> that the form binds to.
/// </summary>
/// <remarks>
/// <para>
/// Blazor keeps one component instance when the route changes between <c>/items/new</c>, <c>/items/{Id}/edit</c> and another id, so the state
/// has to follow the key. Call <see cref="Sync"/> from <c>OnParametersSetAsync</c>: it does nothing while the key is unchanged, starts over
/// when the key changes, and drops a load that finishes after the route has moved on. The page keeps what is specific to it: the form
/// fields, the commands that save, and the navigation after a save. The session does not decide what a save does.
/// </para>
/// <para>
/// <c>[PersistentState]</c> stays on the page, because the attribute only works on a component property. Pass the property through to
/// <see cref="Record"/> so the record restored from the prerendered page is not read again:
/// <c>[PersistentState] public Dto? Existing { get =&gt; _session.Record; set =&gt; _session.Record = value; }</c>.
/// Dispose the session with the page. Use it on the renderer's synchronization context.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the record key.</typeparam>
/// <typeparam name="TForm">The form model; a new instance is used for each key.</typeparam>
/// <typeparam name="TRecord">The loaded record.</typeparam>
public sealed class EditSession<TKey, TForm, TRecord> : IDisposable
    where TKey : struct
    where TForm : class, new()
    where TRecord : class
{
    private readonly ParameterTracker<TKey?> _tracker = new();
    private readonly string[] _knownFields;
    private readonly string _idPrefix;
    private readonly string _noun;
    private TKey? _requested;

    /// <summary>Initializes a session for a form that edits one kind of record.</summary>
    /// <param name="knownFields">The model field names the form can display; see <see cref="ServerFormSubmission.ForRecord"/>.</param>
    /// <param name="idPrefix">The prefix of the control ids; the id of a field is the prefix, a hyphen and the field name.</param>
    /// <param name="noun">The lowercase name of the record, for example <c>customer</c>, used in the default messages.</param>
    /// <exception cref="ArgumentNullException"><paramref name="knownFields"/> is null.</exception>
    /// <exception cref="ArgumentException">The prefix or the noun is blank.</exception>
    public EditSession(IEnumerable<string> knownFields, string idPrefix, string noun)
    {
        ArgumentNullException.ThrowIfNull(knownFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(idPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(noun);

        _knownFields = [.. knownFields];
        _idPrefix = idPrefix;
        _noun = noun;
    }

    /// <summary>Gets the form model; it is replaced by a new instance when the key changes.</summary>
    public TForm Form
    {
        get; private set;
    } = new();

    /// <summary>
    /// Gets or sets the loaded record, or <see langword="null"/> for a new record, a record that is still loading, or one that is missing.
    /// Set it before the first <see cref="Sync"/> to use a record restored from the prerendered page instead of loading it.
    /// </summary>
    public TRecord? Record
    {
        get; set;
    }

    /// <summary>Gets whether the record could not be loaded, so the page shows its "no longer exists" message instead of the form.</summary>
    public bool IsMissing
    {
        get; private set;
    }

    /// <summary>Gets the edit context of <see cref="Form"/>, or <see langword="null"/> until the form is ready to show.</summary>
    public EditContext? Context
    {
        get; private set;
    }

    /// <summary>Gets the submission state of the form, or <see langword="null"/> until the form is ready to show.</summary>
    public ServerFormSubmission? Submission
    {
        get; private set;
    }

    /// <summary>
    /// Follows the route key. Returns at once while the key is unchanged. When it changed, drops the previous form, record and submission,
    /// runs <paramref name="onReset"/>, loads the record for an existing key (unless <see cref="Record"/> already holds it, as it does after a
    /// restore on the first call), fills the form from it and makes <see cref="Context"/> and <see cref="Submission"/> ready.
    /// </summary>
    /// <param name="key">The record key, or <see langword="null"/> for a new record.</param>
    /// <param name="load">Loads the record of a key; a failed result marks the record as missing. It returns a <see cref="ValueTask{TResult}"/>, so <c>Mediator.Send(...)</c> fits directly.</param>
    /// <param name="fill">Copies the record into the form.</param>
    /// <param name="onReset">Runs when the key changed and the previous state was dropped, for the page's own state; optional.</param>
    /// <returns>A task that completes when the session is ready, missing, or superseded by a newer key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="load"/> or <paramref name="fill"/> is null.</exception>
    public async Task Sync(TKey? key, Func<TKey, ValueTask<Result<TRecord>>> load, Action<TForm, TRecord> fill, Action? onReset = null)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(fill);

        var change = _tracker.Update(key);
        if (change == ParameterChange.Unchanged)
        {
            return;
        }

        _requested = key;
        if (change == ParameterChange.Changed)
        {
            Reset();
            onReset?.Invoke();
        }

        if (key is { } id)
        {
            if (Record is null)
            {
                var found = await load(id).ConfigureAwait(true);
                if (!Equals(_requested, key))
                {
                    // The route moved to another record while this one loaded; the newer call owns the session.
                    return;
                }

                if (found.IsFailure)
                {
                    IsMissing = true;
                    return;
                }

                Record = found.Value;
            }

            fill(Form, Record);
        }

        Context = new EditContext(Form);
        Submission = ServerFormSubmission.ForRecord(Context, _knownFields, _idPrefix, _noun);
    }

    private void Reset()
    {
        Submission?.Dispose();
        Submission = null;
        Context = null;
        Record = null;
        IsMissing = false;
        Form = new TForm();
    }

    /// <inheritdoc />
    public void Dispose() => Submission?.Dispose();
}
