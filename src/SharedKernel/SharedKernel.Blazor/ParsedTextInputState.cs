using Microsoft.AspNetCore.Components.Forms;

namespace PandaBear.Blazor;

/// <summary>Retains editable text separately from the last accepted value and reports parsing errors on form validation.</summary>
/// <remarks>Use on the renderer's synchronization context. The caller owns the format, parsing rules, control and error text.</remarks>
/// <typeparam name="TValue">The typed value accepted by the form.</typeparam>
public sealed class ParsedTextInputState<TValue> : IDisposable
{
    private readonly Func<TValue, string?> _format;
    private readonly Func<string, (TValue Value, string? Error)> _parse;
    private EditContext? _context;
    private FieldIdentifier _field;
    private ValidationMessageStore? _messages;
    private TValue _accepted = default!;
    private bool _hasAccepted;
    private string? _problem;

    /// <summary>Creates the input state using caller-supplied formatting and parsing.</summary>
    /// <param name="format">Formats values supplied by the model. A null result is displayed as empty text.</param>
    /// <param name="parse">Parses edited text. Return a null error when the value can be accepted.</param>
    /// <exception cref="ArgumentNullException">The formatter or parser is null.</exception>
    public ParsedTextInputState(Func<TValue, string?> format, Func<string, (TValue Value, string? Error)> parse)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(parse);
        _format = format;
        _parse = parse;
    }

    /// <summary>Gets the text to display in the control, including rejected edits.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Associates the input with its current field and synchronizes the bound value.</summary>
    /// <remarks>Call on parameter updates. An unchanged model value preserves the text, including when the field name changes.</remarks>
    /// <param name="context">The form's edit context.</param>
    /// <param name="field">The field that receives parsing errors.</param>
    /// <param name="value">The value supplied by the model.</param>
    /// <exception cref="ArgumentNullException">The context is null.</exception>
    /// <exception cref="ArgumentException">The field identifier is uninitialized.</exception>
    public void Bind(EditContext context, FieldIdentifier field, TValue value)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (field.Model is null)
        {
            throw new ArgumentException("Provide an initialized field identifier.", nameof(field));
        }

        if (!ReferenceEquals(_context, context))
        {
            Dispose();
            _context = context;
            _messages = new ValidationMessageStore(context);
            _context.OnValidationRequested += Validate;
        }

        _field = field;
        if (!_hasAccepted || !EqualityComparer<TValue>.Default.Equals(value, _accepted))
        {
            Text = _format(value) ?? string.Empty;
            _accepted = value;
            _hasAccepted = true;
            _problem = null;
        }

        PlaceMessage();
    }

    /// <summary>Stores an edit and returns a newly parsed value only when the parser accepts it.</summary>
    /// <param name="text">The new text to display.</param>
    /// <param name="value">The parsed value if accepted, otherwise the default value.</param>
    /// <returns>Whether the caller should write the value back to the model.</returns>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public bool TrySetText(string text, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        var parsed = _parse(text);
        if (parsed.Error is not null)
        {
            value = default!;
            return false;
        }

        value = parsed.Value;
        _accepted = value;
        _hasAccepted = true;
        return true;
    }

    /// <summary>Detaches form validation and clears messages owned by this state.</summary>
    public void Dispose()
    {
        _context?.OnValidationRequested -= Validate;
        _messages?.Clear();
        _context = null;
        _messages = null;
    }

    private void Validate(object? sender, ValidationRequestedEventArgs args)
    {
        var problem = _parse(Text).Error;
        if (problem is null && _problem is null)
        {
            return;
        }

        _problem = problem;
        PlaceMessage();
        _context?.NotifyValidationStateChanged();
    }

    private void PlaceMessage()
    {
        _messages?.Clear();
        if (_problem is not null)
        {
            _messages?.Add(_field, _problem);
        }
    }
}
