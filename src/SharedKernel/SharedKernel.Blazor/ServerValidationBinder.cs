using Microsoft.AspNetCore.Components.Forms;
using PandaBear.HttpClients;

namespace PandaBear.Blazor;

/// <summary>
/// Shows server validation errors on an <see cref="EditContext" /> through a <see cref="ValidationMessageStore" />.
/// </summary>
/// <remarks>
/// Only keys that match a configured known field are shown; names and messages are bounded, and messages that are
/// blank, multi-line, or stack-trace-like are replaced by the configured fallback message. Messages for one field
/// are cleared when that field changes, and all are cleared when a new validation request starts (an
/// <c>EditForm</c> submit), so unchanged data can be submitted again. Server text is never joined into one free-form message. Dispose the binder
/// with the form. Not thread-safe; use it on the renderer's synchronization context.
/// </remarks>
public sealed class ServerValidationBinder : IDisposable
{
    private readonly EditContext _editContext;
    private readonly ServerValidationOptions _options;
    private readonly Dictionary<string, string> _knownFields;
    private readonly ValidationMessageStore _store;

    /// <summary>Initializes a binder for an edit context.</summary>
    /// <param name="editContext">The edit context that displays the messages.</param>
    /// <param name="options">The accepted fields and bounds.</param>
    /// <exception cref="ArgumentNullException">A value is null.</exception>
    public ServerValidationBinder(EditContext editContext, ServerValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(editContext);
        ArgumentNullException.ThrowIfNull(options);

        _editContext = editContext;
        _options = options;
        _knownFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in options.KnownFields)
        {
            _knownFields.TryAdd(field, field);
        }

        _store = new ValidationMessageStore(editContext);
        _editContext.OnFieldChanged += ClearChangedField;
        _editContext.OnValidationRequested += ClearOnValidationRequested;
    }

    /// <summary>Replaces the previous server messages with those of <paramref name="outcome" />.</summary>
    /// <param name="outcome">The outcome; its <see cref="HttpOutcome.ValidationErrors" /> are applied and its message is ignored.</param>
    /// <returns>The placed issues and whether anything could not be placed.</returns>
    public ServerValidationResult Apply(HttpOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return Apply(outcome.ValidationErrors);
    }

    /// <summary>Replaces the previous server messages with <paramref name="errors" />.</summary>
    /// <param name="errors">Server errors keyed by field name, or <see langword="null" /> for none.</param>
    /// <returns>The placed issues and whether anything could not be placed.</returns>
    public ServerValidationResult Apply(IReadOnlyDictionary<string, string[]>? errors)
    {
        _store.Clear();
        var issues = new List<ServerValidationIssue>();
        var unplaced = false;
        var considered = 0;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (key, messages) in errors ?? new Dictionary<string, string[]>())
        {
            if (messages is null || messages.Length == 0)
            {
                continue;
            }

            if (++considered > _options.MaxFields
                || string.IsNullOrEmpty(key)
                || key.Length > _options.MaxFieldNameLength
                || !_knownFields.TryGetValue(key, out var fieldName))
            {
                unplaced = true;
                continue;
            }

            unplaced |= AddMessages(fieldName, messages, issues, counts);
        }

        _editContext.NotifyValidationStateChanged();
        return new ServerValidationResult(issues, unplaced);
    }

    /// <summary>Removes every server message.</summary>
    public void Clear()
    {
        _store.Clear();
        _editContext.NotifyValidationStateChanged();
    }

    /// <summary>Stops reacting to field changes and validation requests. Messages already shown stay until cleared.</summary>
    public void Dispose()
    {
        _editContext.OnFieldChanged -= ClearChangedField;
        _editContext.OnValidationRequested -= ClearOnValidationRequested;
    }

    private bool AddMessages(string fieldName, string[] messages, List<ServerValidationIssue> issues, Dictionary<string, int> counts)
    {
        var field = _editContext.Field(fieldName);
        foreach (var raw in messages)
        {
            counts.TryGetValue(fieldName, out var count);
            if (count >= _options.MaxMessagesPerField)
            {
                return true;
            }

            var message = BoundedText.ToSingleLineMessage(raw, _options.MaxMessageLength) ?? _options.FallbackMessage;
            if (issues.Any(issue => issue.FieldName == fieldName && string.Equals(issue.Message, message, StringComparison.Ordinal)))
            {
                continue;
            }

            counts[fieldName] = count + 1;
            _store.Add(field, message);
            issues.Add(new ServerValidationIssue(fieldName, message));
        }

        return false;
    }

    private void ClearOnValidationRequested(object? sender, ValidationRequestedEventArgs e) => _store.Clear();

    private void ClearChangedField(object? sender, FieldChangedEventArgs e)
    {
        _store.Clear(e.FieldIdentifier);
        _editContext.NotifyValidationStateChanged();
    }
}
