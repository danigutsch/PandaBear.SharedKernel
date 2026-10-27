namespace PandaBear.Blazor;

/// <summary>Configures which server validation fields are accepted and how much text is kept.</summary>
public sealed record ServerValidationOptions
{
    /// <summary>Initializes options.</summary>
    /// <param name="knownFields">The model field names the form can display; keys are matched without regard to case.</param>
    /// <param name="fallbackMessage">The nonblank field message that replaces any server message that is blank, multi-line, or stack-trace-like.</param>
    /// <exception cref="ArgumentNullException">A value is null.</exception>
    /// <exception cref="ArgumentException">The fallback message is blank.</exception>
    public ServerValidationOptions(IEnumerable<string> knownFields, string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(knownFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackMessage);

        KnownFields = [.. knownFields];
        FallbackMessage = fallbackMessage;
    }

    /// <summary>Gets the model field names the form can display.</summary>
    public IReadOnlyCollection<string> KnownFields
    {
        get;
    }

    /// <summary>Gets the field message that replaces unusable server messages.</summary>
    public string FallbackMessage
    {
        get;
    }

    /// <summary>Gets the longest server field name that is considered, 128 by default.</summary>
    public int MaxFieldNameLength { get; init; } = 128;

    /// <summary>Gets the longest kept message, 300 characters by default; longer messages end in an ellipsis.</summary>
    public int MaxMessageLength { get; init; } = 300;

    /// <summary>Gets how many messages are kept per field, 5 by default.</summary>
    public int MaxMessagesPerField { get; init; } = 5;

    /// <summary>Gets how many server keys are considered, 50 by default; further keys count as unplaced.</summary>
    public int MaxFields { get; init; } = 50;
}
