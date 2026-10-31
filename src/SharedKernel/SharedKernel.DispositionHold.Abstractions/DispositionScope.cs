namespace PandaBear.DispositionHold;

/// <summary>
/// Identifies what a hold restrains: an opaque kind (for example a record class) and an opaque key within it.
/// Neither part may carry personal data.
/// </summary>
public readonly record struct DispositionScope
{
    /// <summary>The maximum length of <see cref="Kind" />.</summary>
    public const int MaxKindLength = 64;

    /// <summary>The maximum length of <see cref="Key" />.</summary>
    public const int MaxKeyLength = 128;

    private readonly string? kind;
    private readonly string? key;

    private DispositionScope(string kind, string key)
    {
        this.kind = kind;
        this.key = key;
    }

    /// <summary>Gets the opaque scope kind.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the scope was not created through <see cref="From" />.</exception>
    public string Kind => kind ?? throw new InvalidOperationException(
        "DispositionScope must be created through DispositionScope.From before it can be used.");

    /// <summary>Gets the opaque key of the restrained record or group within <see cref="Kind" />.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the scope was not created through <see cref="From" />.</exception>
    public string Key => key ?? throw new InvalidOperationException(
        "DispositionScope must be created through DispositionScope.From before it can be used.");

    /// <summary>Creates a scope from opaque identifiers.</summary>
    /// <param name="kind">The scope kind.</param>
    /// <param name="key">The key within the kind.</param>
    /// <returns>The created scope.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a value is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a value is empty, too long or contains a character other than ASCII letters, digits and <c>. _ : -</c>.</exception>
    public static DispositionScope From(string? kind, string? key) => new(
        DispositionIdentifier.Require(kind, nameof(kind), MaxKindLength),
        DispositionIdentifier.Require(key, nameof(key), MaxKeyLength));

    /// <inheritdoc />
    public override string ToString() => $"{Kind}/{Key}";
}
